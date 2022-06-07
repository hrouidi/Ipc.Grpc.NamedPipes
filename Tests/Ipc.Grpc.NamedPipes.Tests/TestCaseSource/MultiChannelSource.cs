using Ipc.Grpc.NamedPipes.Tests.Helpers;
using System.Collections;
using System.Collections.Generic;

namespace Ipc.Grpc.NamedPipes.Tests.TestCaseSource
{
    public class MultiChannelSource : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { new HttpChannelContextFactory() };
            yield return new object[] { new GrpcDotNetNamedPipesChannelFactory() };
            yield return new object[] { new NamedPipeChannelContextFactory() };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

}