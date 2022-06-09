using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class ServerListener : IDisposable
    {
        private readonly string _pipeName;
        private readonly CancellationTokenSource _shutdownCancellationTokenSource;
        private readonly NamedPipeServerOptions _options;
        private readonly List<Task> _listenerTasks;
        private readonly List<Task> _connectionsTasks;

        private readonly IReadOnlyDictionary<string, Func<ServerConnection, ValueTask>> _methodHandlers;

        private volatile bool _started;
        private volatile bool _disposed;

        public ServerListener(string pipeName, NamedPipeServerOptions options, IReadOnlyDictionary<string, Func<ServerConnection, ValueTask>> methodHandlers)
        {
            _pipeName = pipeName;
            _options = options;
            _methodHandlers = methodHandlers;
            _shutdownCancellationTokenSource = new CancellationTokenSource();
            _listenerTasks = new List<Task>();
            _connectionsTasks = new List<Task>();
        }

        public void Start(int poolSize = 100)
        {
            CheckIfDisposed();
            if (_started == false)
            {
                for (int i = 0; i < poolSize; i++)
                {
                    //var thread = new Thread(ListenConnectionsAsync);
                    //thread.Start();

                    Task task = Task.Factory.StartNew(async () => await ListenConnectionsAsync());
                    _listenerTasks.Add(task);
                }
                Task.WaitAll(_listenerTasks.ToArray());
                _started = true;
            }
        }

        public void Stop()//Blocking stop
        {
            CheckIfDisposed();
            if (_started)
            {
                _started = false;
                _shutdownCancellationTokenSource.Cancel();
                Task.WaitAll(_listenerTasks.ToArray());
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _shutdownCancellationTokenSource.Cancel();
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

        private async Task ListenConnectionsAsync()
        {
            while (true)
            {
                NamedPipeServerStream pipeServer = CreatePipeServer();
                try
                {
                    await pipeServer.WaitForConnectionAsync(_shutdownCancellationTokenSource.Token).ConfigureAwait(false);
                    _ = HandleConnectionAsync(pipeServer);
                }
                catch (Exception ex)
                {
                    pipeServer.Dispose();
                    if (_shutdownCancellationTokenSource.IsCancellationRequested)
                        break;
                    Console.WriteLine($"Error: {ex.Message}");
                    throw;
                }
            }
        }

        private async Task HandleConnectionAsync(NamedPipeServerStream pipeServer)
        {
            await Task.Yield();
            try
            {
                using var connection = new ServerConnection(pipeServer, _methodHandlers);
                await connection.ListenMessagesAsync(_shutdownCancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private void CheckIfDisposed()
        {
            const string msg = "The server has been killed and can't be restarted. Create a new server if needed.";
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServerListener), msg);
        }
    }
}