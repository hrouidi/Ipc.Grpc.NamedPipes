#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class ServerListener : IDisposable
    {
        private readonly CancellationTokenSource _shutdownCancellationTokenSource;
        private readonly IPool<ServerConnection> _serverConnectionsPool;
        private readonly TaskCompletionSource<bool> _listenerReadyTask;

        private volatile bool _started;
        private volatile bool _disposed;
        private Task? _listeningTask;

        public ServerListener(IPool<ServerConnection> serverConnectionsPool, CancellationTokenSource shutdownCancellationTokenSource)
        {
            _serverConnectionsPool = serverConnectionsPool;
            _shutdownCancellationTokenSource = shutdownCancellationTokenSource;
            _listenerReadyTask = new TaskCompletionSource<bool>();
        }

        public async Task StartAsync()
        {
            CheckIfDisposed();
            if (_started == false)
            {
                _listeningTask = Task.Factory.StartNew(ListenConnectionsAsync, TaskCreationOptions.LongRunning);
                await _listenerReadyTask.Task.ConfigureAwait(false);
                _started = true;
            }
        }

        public async Task RunAsync()
        {
            CheckIfDisposed();
            await StartAsync().ConfigureAwait(false);
            await _listeningTask!.ConfigureAwait(false);
        }

        public async Task ShutdownAsync()
        {
            CheckIfDisposed();
            if (_started)
            {
                _started = false;
                _shutdownCancellationTokenSource.Cancel();
                await _listeningTask!.ConfigureAwait(false);
            }
        }

        public void Dispose()//Kill
        {
            _disposed = true;
            _shutdownCancellationTokenSource.Cancel();
            _serverConnectionsPool.Dispose();
        }

        //Has dedicate thread for connection handling throughput
        //Should never throw
        private void ListenConnectionsAsync()
        {
            while (true)
            {
                //Console.WriteLine($"####  thread id: {Thread.CurrentThread.ManagedThreadId}");
                ServerConnection? connection = null;
                try
                {
                    connection = _serverConnectionsPool.Rent();
                    _listenerReadyTask.TrySetResult(true);
                    connection.WaitForClientConnection();
                    //Console.WriteLine($"####  after connected thread id: {Thread.CurrentThread.ManagedThreadId}");
                    _ = Task.Run(() => AcceptClientConnectionAsync(connection), _shutdownCancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    if (_shutdownCancellationTokenSource.IsCancellationRequested)
                        break;
                    _serverConnectionsPool.Return(connection);   //replace it by a new one in the pool
                    Console.WriteLine($"{nameof(ServerListener)} Error while WaitForConnectionAsync: {ex.Message}");
                }
            }
        }

        private async Task AcceptClientConnectionAsync(ServerConnection connection)//should never throw
        {
            try
            {
                await connection.HandleClientMessagesAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"{nameof(ServerListener)} Error while ListenMessagesAsync: {ex.Message}");
            }
            finally
            {
                _serverConnectionsPool.Return(connection);
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