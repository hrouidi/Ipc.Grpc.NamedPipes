
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers
{
    public abstract class ChannelContextFactory
    {
        public abstract ChannelContext Create();
        public abstract TestService.TestServiceClient CreateClient();
    }
}