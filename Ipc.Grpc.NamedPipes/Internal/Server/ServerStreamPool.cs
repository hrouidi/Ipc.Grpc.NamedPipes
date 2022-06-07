using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class ServerStreamPool : IDisposable
    {
        private const int PoolSize = 4;

        private readonly string _pipeName;
        private readonly CancellationTokenSource _cts;
        private readonly NamedPipeServerOptions _options;
        private readonly List<Task> _runningTasks;

        private readonly IReadOnlyDictionary<string, Func<ServerConnectionContext, ValueTask>> _methodHandlers;

        private volatile bool _started;
        private volatile bool _stoped;
        private volatile bool _disposed;

        public ServerStreamPool(string pipeName, NamedPipeServerOptions options, IReadOnlyDictionary<string, Func<ServerConnectionContext, ValueTask>> methodHandlers)
        {
            _pipeName = pipeName;
            _options = options;
            _methodHandlers = methodHandlers;
            _cts = new CancellationTokenSource();
            _runningTasks = new List<Task>();
        }

        public void Start()
        {
            CheckIfDisposed();
            if (_started == false)
            {
                for (int i = 0; i < PoolSize; i++)
                {
                    //var thread = new Thread(ConnectionLoop);
                    //thread.Start();
                    Task task = Task.Factory.StartNew(HandleConnectionAsync, TaskCreationOptions.LongRunning);
                    _runningTasks.Add(task);
                }

                _started = true;
            }
        }

        public async Task StartAsync()
        {
            CheckIfDisposed();
            if (_started == false)
            {
                List<Task> runningTasks = new List<Task>(PoolSize);
                for (int i = 0; i < PoolSize; i++)
                {
                    Task task = Task.Factory.StartNew(HandleConnectionAsync, TaskCreationOptions.LongRunning);
                    runningTasks.Add(task);
                }

                await Task.WhenAll(runningTasks.ToArray()).ConfigureAwait(false);
                _started = true;
            }
        }

        //TODO : Fix this
        public void Stop()
        {
            CheckIfDisposed();
            _stoped = true;
            _cts.Cancel();
            Task.WaitAll(_runningTasks.ToArray());
        }
        //TODO : Fix this
        public Task StopAsync()
        {
            CheckIfDisposed();
            _stoped = true;
            _cts.Cancel();
            return Task.WhenAll(_runningTasks.ToArray());
        }

        public void Dispose()
        {
            _disposed = true;
            try
            {
                _cts.Cancel();
                //_cts.Dispose();
            }
            catch (Exception)
            {
                // TODO: Log
            }
        }

        private NamedPipeServerStream CreatePipeServer()
        {
            var pipeOptions = PipeOptions.Asynchronous;
#if NETCOREAPP || NETSTANDARD
#if !NETSTANDARD2_0
            if (_options.CurrentUserOnly)
            {
                pipeOptions |= PipeOptions.CurrentUserOnly;
            }
#endif

#if NET5_0
            return NamedPipeServerStreamAcl.Create(_pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Message,
                pipeOptions,
                0,
                0,
                _options.PipeSecurity);
#else
            return new NamedPipeServerStream(_pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Message,
                pipeOptions);
#endif
#endif
#if NETFRAMEWORK
            return new NamedPipeServerStream(_pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Message,
                pipeOptions,
                0,
                0,
                _options.PipeSecurity);
#endif
        }

        private async void HandleConnectionAsync()
        {
            while (true)
            {
                NamedPipeServerStream pipeServer = CreatePipeServer();
                try
                {
                    await pipeServer.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);
                    var ctx = new ServerConnectionContext(pipeServer, _methodHandlers);
                    await ctx.ReadLoop().ConfigureAwait(false);

                    pipeServer.Disconnect();
                }
                catch (Exception)
                {
                    if (_cts.IsCancellationRequested)
                    {
                        break;
                    }
                }
                finally
                {
                    pipeServer.Dispose();
                }
            }
        }

        private void CheckIfDisposed()
        {
            const string msg = "The server has been killed and can't be restarted. Create a new server if needed.";
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServerStreamPool), msg);
        }
    }
}