using Ipc.Grpc.NamedPipes.Tests.Helpers;
using System.Collections;
using System.Collections.Generic;

namespace Ipc.Grpc.NamedPipes.Tests.TestCaseSource
{
    public class GrpcDotNetNamedPipesChannelSource : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { new GrpcDotNetNamedPipesChannelFactory() };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
