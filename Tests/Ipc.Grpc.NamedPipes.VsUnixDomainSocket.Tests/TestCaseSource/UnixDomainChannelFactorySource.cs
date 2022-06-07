using System.Collections;
using System.Collections.Generic;
using Ipc.Grpc.NamedPipes.VsUnixDomainSocket.Tests.Helpers;

namespace Ipc.Grpc.NamedPipes.VsUnixDomainSocket.Tests.TestCaseSource
{
    class UnixDomainChannelFactorySource : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { new UnixDomainChannelContextFactory() };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
