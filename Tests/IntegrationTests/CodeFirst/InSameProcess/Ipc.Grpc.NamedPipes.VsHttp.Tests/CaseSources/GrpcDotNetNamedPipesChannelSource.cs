﻿using System.Collections;
using System.Collections.Generic;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests.CaseSources
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
