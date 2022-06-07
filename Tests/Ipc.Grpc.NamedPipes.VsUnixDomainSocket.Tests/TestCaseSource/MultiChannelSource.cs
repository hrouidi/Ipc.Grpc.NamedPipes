using System.Collections;
using System.Collections.Generic;
using Ipc.Grpc.NamedPipes.Tests.Helpers;
using Ipc.Grpc.NamedPipes.VsUnixDomainSocket.Tests.Helpers;

namespace Ipc.Grpc.NamedPipes.VsUnixDomainSocket.Tests.TestCaseSource
{
    public class MultiChannelSource : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            //yield return new object[] {new HttpChannelContextFactory()};
            yield return new object[] { new UnixDomainChannelContextFactory() };
            yield return new object[] { new GrpcDotNetNamedPipesChannelFactory() };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}