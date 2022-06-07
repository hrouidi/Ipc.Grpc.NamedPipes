using System;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.Tests.ProtoContract;

namespace Ipc.Grpc.NamedPipes.Tests.Helpers
{
    public class ChannelContext : IDisposable
    {
        public Action OnDispose { get; set; }

        public TestService.TestServiceClient Client { get; set; }

        public TestServiceImplementation Impl { get; set; }

        public void Dispose()
        {
            OnDispose.Invoke();
        }
    }
}