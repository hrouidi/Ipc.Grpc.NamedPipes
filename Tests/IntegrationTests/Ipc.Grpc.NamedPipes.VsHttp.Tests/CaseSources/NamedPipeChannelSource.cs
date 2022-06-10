using System.Collections;
using System.Collections.Generic;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests.TestCaseSource
{
    public class NamedPipeChannelSource : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { new NamedPipeChannelContextFactory() };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
