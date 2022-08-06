using System;
using System.Threading.Tasks;
using Ipc.Grpc.NamedPipes.Tests.ProtoContract;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.CaseSources;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
using NUnit.Framework;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests
{
    public class GoogleMarshallerTests
    {
        public const int TestTimeout = 3000;

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(NamedPipeChannelSource))]
        public async Task TestAsyncTest(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            ResponseMessage response = await ctx.Client.TestAsync(new ProtoMessage
            {
                Name = "test",
                Date = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
            });
            Assert.That(response.Value, Is.EqualTo(0));
        }
    }
}