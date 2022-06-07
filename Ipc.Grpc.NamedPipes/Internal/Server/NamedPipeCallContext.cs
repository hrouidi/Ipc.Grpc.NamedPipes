using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;

namespace Ipc.Grpc.NamedPipes.Internal
{
    public class NamedPipeCallContext : ServerCallContext
    {
        private readonly ServerConnectionContext _serverConnectionContext;

        internal NamedPipeCallContext(ServerConnectionContext ctx)
        {
            _serverConnectionContext = ctx;
        }

        protected override CancellationToken CancellationTokenCore => _serverConnectionContext.CancellationTokenSource.Token;

        protected override async Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        {
            await _serverConnectionContext.Transport.SendResponseHeaders(responseHeaders, _serverConnectionContext.CancellationTokenSource.Token)
                                           .ConfigureAwait(false);
        }

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions options) => throw new NotSupportedException();

        protected override string MethodCore => throw new NotSupportedException();

        protected override string HostCore => throw new NotSupportedException();

        protected override string PeerCore => throw new NotSupportedException();

        protected override DateTime DeadlineCore => _serverConnectionContext.Deadline.Value;

        protected override Metadata RequestHeadersCore => _serverConnectionContext.RequestHeaders;

        protected override Metadata ResponseTrailersCore { get; } = new Metadata();

        protected override Status StatusCore { get; set; }

        protected override WriteOptions WriteOptionsCore { get; set; }

        protected override AuthContext AuthContextCore => throw new NotSupportedException();
    }
}