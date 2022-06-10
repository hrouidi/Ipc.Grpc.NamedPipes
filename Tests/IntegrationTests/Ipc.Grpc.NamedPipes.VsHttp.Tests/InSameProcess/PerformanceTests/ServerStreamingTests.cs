using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.TestCaseSource;
using NUnit.Framework;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests.InSameProcess.PerformanceTests
{
    public class ServerStreamingTests
    {
        public const int TestTimeout = 10 * 1000;
        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ServerStreamingManyMessagesPerformance(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            var stopwatch = Stopwatch.StartNew();
            var call = ctx.Client.ServerStreaming(new RequestMessage { Value = 10_000 });
            while (await call.ResponseStream.MoveNext())
            {
            }

            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        //TODO : cover more cases

    }
}