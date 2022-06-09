using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;

namespace Ipc.Grpc.NamedPipes.Internal
{
    public class NamedPipeCallContext : ServerCallContext
    {
        private readonly ServerConnection _connection;

        internal NamedPipeCallContext(ServerConnection connection) => _connection = connection;

        protected override CancellationToken CancellationTokenCore => _connection.CancellationTokenSource.Token;

        protected override async Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        {
            await _connection.SendResponseHeaders(responseHeaders)
                             .ConfigureAwait(false);
        }

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions options) => throw new NotSupportedException();

        protected override string MethodCore => throw new NotSupportedException();

        protected override string HostCore => throw new NotSupportedException();

        protected override string PeerCore => throw new NotSupportedException();

        protected override DateTime DeadlineCore => _connection.Deadline.Value;

        protected override Metadata RequestHeadersCore => _connection.RequestHeaders;

        protected override Metadata ResponseTrailersCore { get; } = new Metadata();

        protected override Status StatusCore { get; set; }

        protected override WriteOptions WriteOptionsCore { get; set; }

        protected override AuthContext AuthContextCore => throw new NotSupportedException();
    }
}