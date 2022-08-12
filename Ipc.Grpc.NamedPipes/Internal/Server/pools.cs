#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal interface IPool<TItem> : IDisposable where TItem : IDisposable
    {
        TItem Rent();
        void Return(TItem? item);
    }

    internal class PipePool : IPool<NamedPipeServerStream>
    {
        private readonly string _pipeName;
        private readonly NamedPipeServerOptions _options;
        private readonly ConcurrentQueue<NamedPipeServerStream> _pipes;

        public PipePool(string pipeName, NamedPipeServerOptions options)
        {
            _pipeName = pipeName;
            _options = options;
            _pipes = new ConcurrentQueue<NamedPipeServerStream>(Enumerable.Range(0, options.ConnectionPoolSize).Select(x => CreatePipeServer()));
        }

        public NamedPipeServerStream Rent()
        {
            if (_pipes.TryDequeue(out NamedPipeServerStream? item))
                return item;
            return CreatePipeServer();
        }

        public void Return(NamedPipeServerStream? pipe) //replace by a new one !
        {
            pipe?.Dispose();
            _pipes.Enqueue(CreatePipeServer());
        }

        public void Dispose()
        {
            foreach (NamedPipeServerStream pipe in _pipes)
                SafeDispose(pipe);
        }

        private void SafeDispose(IDisposable pipe)
        {
            try
            {
                pipe.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{nameof(PipePool)}: disposing pipes error ,[Pool size]={_pipes.Count}, [Error] ={ex.Message}");
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
    }

    internal class ServerConnectionsPool : IPool<ServerConnection>
    {
        private readonly IPool<NamedPipeServerStream> _pipePool;
        private readonly CancellationToken _shutdownToken;
        private readonly IReadOnlyDictionary<string, Func<ServerConnection, ValueTask>> _methodHandlers;

        private readonly ConcurrentQueue<ServerConnection> _connections;
        public ServerConnectionsPool(int poolSize, IPool<NamedPipeServerStream> pipePool, IReadOnlyDictionary<string, Func<ServerConnection, ValueTask>> methodHandlers, CancellationToken shutdownToken)
        {
            _pipePool = pipePool;
            _shutdownToken = shutdownToken;
            _methodHandlers = methodHandlers;
            _connections = new ConcurrentQueue<ServerConnection>(Enumerable.Range(0, poolSize).Select(x => CreateServerConnection()));
        }

        public ServerConnection Rent()
        {
            if (_connections.TryDequeue(out ServerConnection? item))
                return item;
            return CreateServerConnection();
        }

        public void Return(ServerConnection? connection)// Try recycle this connection
        {
            connection?.Dispose();
            _connections.Enqueue(CreateServerConnection());
        }

        public void Dispose()
        {
            foreach (ServerConnection connection in _connections)
                SafeDispose(connection);
        }

        private void SafeDispose(ServerConnection connection)
        {
            try
            {
                connection.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{nameof(ServerConnectionsPool)}: disposing server connections error ,[Pool size]={_connections.Count}, [Error] ={ex.Message}");
            }
        }

        private ServerConnection CreateServerConnection()
        {
            return new ServerConnection(_pipePool, _methodHandlers, _shutdownToken);
        }
    }
}