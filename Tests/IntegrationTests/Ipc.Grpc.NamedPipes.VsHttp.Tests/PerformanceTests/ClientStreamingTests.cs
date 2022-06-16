using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.CaseSources;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
using NUnit.Framework;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests.InSameProcess.PerformanceTests
{
    public class ClientStreamingTests
    {
        public const int TestTimeout = 10 * 1000;
        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ClientStreamingManyMessagesPerformance(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();

            var stopwatch = Stopwatch.StartNew();

            var call = ctx.Client.ClientStreaming();
            for (int i = 0; i <= 10_000; i++)
            {
                await call.RequestStream.WriteAsync(new RequestMessage { Value = i });
            }
            await call.RequestStream.CompleteAsync();

            ResponseMessage response = await call.ResponseAsync;
            stopwatch.Stop();

            Assert.That(response.Value, Is.EqualTo(50005000));
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        //TODO : cover more cases
    }
}