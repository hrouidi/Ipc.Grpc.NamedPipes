using System;
using Ipc.Grpc.NamedPipes.Tests.ProtoContract;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers
{
    public class ChannelContext : IDisposable
    {
        public Action OnDispose { get; set; }

        public ITestService Client { get; set; }

        public TestServiceImplementation Impl { get; set; }

        public void Dispose()
        {
            OnDispose.Invoke();
        }
    }
}