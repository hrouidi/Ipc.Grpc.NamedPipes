using System.Collections;
using System.Collections.Generic;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests.TestCaseSource
{
    public class MultiChannelSource : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { new NamedPipeChannelContextFactory() };
            yield return new object[] { new GrpcDotNetNamedPipesChannelFactory() };
            yield return new object[] { new HttpChannelContextFactory() };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

}