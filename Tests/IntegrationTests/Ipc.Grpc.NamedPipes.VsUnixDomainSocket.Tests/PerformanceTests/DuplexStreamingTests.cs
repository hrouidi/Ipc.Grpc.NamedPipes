using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
using NUnit.Framework;
using MultiChannelSource = Ipc.Grpc.NamedPipes.VsUnixDomainSocket.Tests.CaseSources.MultiChannelSource;

namespace Ipc.Grpc.NamedPipes.VsUnixDomainSocket.Tests.InSameProcess.PerformanceTests
{
    public class DuplexStreamingTests
    {
        public const int TestTimeout = 10 * 1000;
        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task DuplexStreamingManyMessagesPerformance(ChannelContextFactory factory)
        {

            using ChannelContext ctx = factory.Create();

            var stopwatch = Stopwatch.StartNew();

            var call = ctx.Client.DuplexStreaming();
            for (int i = 0; i <= 10_000; i++)
            {
                await call.RequestStream.WriteAsync(new RequestMessage { Value = i });
                await call.ResponseStream.MoveNext();
            }
            await call.RequestStream.CompleteAsync();


            stopwatch.Stop();


            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        //TODO : cover more cases
    }
}