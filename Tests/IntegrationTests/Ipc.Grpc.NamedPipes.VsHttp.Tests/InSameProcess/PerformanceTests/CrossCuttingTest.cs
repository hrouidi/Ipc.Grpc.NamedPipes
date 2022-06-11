using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.CaseSources;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
using NUnit.Framework;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests.InSameProcess.PerformanceTests
{
    public class CrossCuttingTest
    {
       
        public const int TestTimeout = 10 * 1000;

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ChannelColdStartPerformance(ChannelContextFactory factory)
        {
            // Note: This test needs to be run on its own for accurate cold start measurements.
            var stopwatch = Stopwatch.StartNew();
            using var ctx = factory.Create();
            await ctx.Client.SimpleUnaryAsync(new RequestMessage());
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }
        

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ChannelWarmStartPerformance(ChannelContextFactory factory)
        {
            using var tempChannel = factory.Create();
            var stopwatch = Stopwatch.StartNew();
            using var ctx = factory.Create();
            await ctx.Client.SimpleUnaryAsync(new RequestMessage());
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }
    }
}