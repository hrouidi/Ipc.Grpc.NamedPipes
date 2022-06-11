using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Google.Protobuf;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.CaseSources;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
using NUnit.Framework;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests.InSameProcess.PerformanceTests
{
    public class UnaryAsyncTest
    {

        [Test]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task Channels_Sequential_Performance(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            List<ResponseMessage> rets = new List<ResponseMessage>(1_000);
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 1_000; i++)
            {
                var client = factory.CreateClient();
                ResponseMessage ret = await client.SimpleUnaryAsync(new RequestMessage());
                rets.Add(ret);
            }

            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        [Test]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task Calls_Sequential_Performance(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            //ByteString byteString = ByteString.CopyFrom(new byte[16*1024]);
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 1_0000; i++)
            {
                ResponseMessage rep = await ctx.Client.SimpleUnaryAsync(new RequestMessage { Value = 123 });
            }

            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        [Test]
        [TestCaseSource(typeof(MultiChannelSource))]

        public async Task Channels_Parallel_Performance(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var stopwatch = Stopwatch.StartNew();
            var tasks = new Task[100];
            for (int i = 0; i < tasks.Length; i++)
            {
                var client = factory.CreateClient();
                tasks[i] = client.SimpleUnaryAsync(new RequestMessage()).ResponseAsync;
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        [Test]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task Calls_Parallel_Performance(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var stopwatch = Stopwatch.StartNew();
            var tasks = new Task[100];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = ctx.Client.SimpleUnaryAsync(new RequestMessage()).ResponseAsync;
            }
            //Task.Delay(1000).Wait();
            await Task.WhenAll(tasks);
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        [Test]
        [TestCaseSource(typeof(MultiChannelSource))]
        //[TestCaseSource(typeof(NamedPipeClassData2))]
        public async Task LargePayloadPerformance(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();

            var bytes = new byte[300 * 1024 * 1024];
            ByteString byteString = UnsafeByteOperations.UnsafeWrap(bytes);
            ResponseMessage ret = null;
            var stopwatch = Stopwatch.StartNew();
            //for (int i = 0; i < 1000; i++)
            ret = await ctx.Client.SimpleUnaryAsync(new RequestMessage { Binary = byteString });
            stopwatch.Stop();
            Assert.That(ret.Binary, Is.EqualTo(byteString));
            Console.WriteLine($" Elapsed :{stopwatch.Elapsed}");
        }

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