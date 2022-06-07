using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Google.Protobuf;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
using Ipc.Grpc.NamedPipes.VsUnixDomainSocket.Tests.TestCaseSource;
using NUnit.Framework;

namespace Ipc.Grpc.NamedPipes.VsUnixDomainSocket.Tests
{
    public class UnaryAsyncPerformanceTest
    {

        [Test]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task Channels_Parallel_Performance(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var stopwatch = Stopwatch.StartNew();
            var tasks = new Task[24];
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
                ResponseMessage rep = await ctx.Client.SimpleUnaryAsync(new RequestMessage { Value = 123});
            }

            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        [Test]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task Calls_Parallel_Performance(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var stopwatch = Stopwatch.StartNew();
            var tasks = new Task[24];
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
        public async Task LargePayloadPerformance(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            
            var bytes = new byte[200 * 1024 * 1024];
            ByteString byteString = ByteString.CopyFrom(bytes);
            var stopwatch = Stopwatch.StartNew();
            ResponseMessage ret = await ctx.Client.SimpleUnaryAsync(new RequestMessage { Binary = byteString });
            stopwatch.Stop();
            Assert.AreEqual(byteString, ret.Binary);
            Console.WriteLine($" Elapsed :{stopwatch.Elapsed}");
        }

    }
}