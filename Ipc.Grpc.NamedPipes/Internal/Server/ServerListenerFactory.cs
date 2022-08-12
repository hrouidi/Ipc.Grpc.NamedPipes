#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal static class ServerListenerFactory
    {
        public static ServerListener Create(string pipeName, NamedPipeServerOptions options, IReadOnlyDictionary<string, Func<ServerConnection, ValueTask>> methodHandlers)
        {
            CancellationTokenSource shutdownCts = new();
            PipePool pipePool = new(pipeName, options);
            ServerConnectionsPool serverConnectionsPool = new(options.ConnectionPoolSize, pipePool, methodHandlers, shutdownCts.Token);
            ServerListener ret = new(serverConnectionsPool, shutdownCts);
            return ret;
        }
    }
}