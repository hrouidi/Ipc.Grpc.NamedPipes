
using Ipc.Grpc.NamedPipes.Tests.ProtoContract;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers
{
    public abstract class ChannelContextFactory
    {
        public abstract ChannelContext Create();
        public abstract ITestService CreateClient();
    }
}