#nullable enable
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
        private readonly PipePool _pipePool;
        private readonly TaskCompletionSource<bool> _listenerReadyTask;
        private readonly IReadOnlyDictionary<string, Func<ServerConnection, ValueTask>> _methodHandlers;

        private volatile bool _started;
        private volatile bool _disposed;

        //TODO: to remove
        public Task ListeningTask { get; private set; }

        public ServerListener(string pipeName, NamedPipeServerOptions options, IReadOnlyDictionary<string, Func<ServerConnection, ValueTask>> methodHandlers)
        {
            _pipeName = pipeName;
            _options = options;
            _methodHandlers = methodHandlers;
            _shutdownCancellationTokenSource = new CancellationTokenSource();
            _listenerReadyTask = new TaskCompletionSource<bool>();

            _pipePool = new PipePool(CreatePipeServer, options.ConnectionPoolSize);
        }

        public async Task StartAsync()
        {
            CheckIfDisposed();
            if (_started == false)
            {
                ListeningTask = Task.Factory.StartNew(() => ListenConnectionsAsync(), TaskCreationOptions.LongRunning);
                await _listenerReadyTask.Task.ConfigureAwait(false);
                _started = true;
            }
        }

        public async Task RunAsync()
        {
            await StartAsync().ConfigureAwait(false);
            await ListeningTask.ConfigureAwait(false);
        }

        public async Task ShutdownAsync()
        {
            CheckIfDisposed();
            if (_started)
            {
                _started = false;
                _shutdownCancellationTokenSource.Cancel();
                await ListeningTask.ConfigureAwait(false);
            }
        }

        public void Dispose()//Kill
        {
            _disposed = true;
            _shutdownCancellationTokenSource.Cancel();
            _pipePool.Dispose();
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

        private void ListenConnectionsAsync()//Should never throw
        {
            while (true)
            {
                //Console.WriteLine($"####  thread id: {Thread.CurrentThread.ManagedThreadId}");
                NamedPipeServerStream? pipeServer = null;
                try
                {
                    pipeServer = _pipePool.Get();
                    _listenerReadyTask.TrySetResult(true);
                    pipeServer.WaitForConnectionAsync(_shutdownCancellationTokenSource.Token)
                              .GetAwaiter()
                              .GetResult();
                    //Console.WriteLine($"####  after connected thread id: {Thread.CurrentThread.ManagedThreadId}");
                    _ = HandleConnectionAsync(pipeServer);
                }
                catch (Exception ex)
                {
                    if (_shutdownCancellationTokenSource.IsCancellationRequested)
                        break;
                    pipeServer?.Dispose();//dispose dirty/broken pipe
                    _pipePool.AddNew();   //replace it by a new one in the pool
                    Console.WriteLine($"{nameof(ServerListener)} Error while WaitForConnectionAsync: {ex.Message}");
                }
            }
        }

        private async Task HandleConnectionAsync(NamedPipeServerStream pipeServer)//should never throw
        {
            await Task.Yield();
            using ServerConnection connection = new(pipeServer, _methodHandlers, _shutdownCancellationTokenSource.Token);
            try
            {
                await connection.ListenMessagesAsync().ConfigureAwait(false);
                await connection.RequestHandlerTask!.ConfigureAwait(false);

            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"{nameof(ServerListener)} Error while ListenMessagesAsync: {ex.Message}");
            }
            finally
            {
                _pipePool.AddNew();
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