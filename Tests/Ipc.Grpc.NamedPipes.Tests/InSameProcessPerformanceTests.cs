
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.Tests.Helpers;
using Ipc.Grpc.NamedPipes.Tests.TestCaseSource;
using NUnit.Framework;

namespace Ipc.Grpc.NamedPipes.Tests
{
    public class InSameProcessPerformanceTests
    {
        private const int TestTimeout = 5000;
        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ServerStreamingManyMessagesPerformance(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var stopwatch = Stopwatch.StartNew();
            var call = ctx.Client.ServerStreaming(new RequestMessage { Value = 10_000 });
            while (await call.ResponseStream.MoveNext())
            {
            }

            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public void UnarySequentialChannelsPerformance(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 1_000; i++)
            {
                var client = factory.CreateClient();
                client.SimpleUnary(new RequestMessage());
            }

            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task UnaryParallelChannelsPerformance(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var stopwatch = Stopwatch.StartNew();
            var tasks = new Task[1_000];
            for (int i = 0; i < tasks.Length; i++)
            {
                var client = factory.CreateClient();
                tasks[i] = client.SimpleUnaryAsync(new RequestMessage()).ResponseAsync;
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public void UnarySequentialCallsPerformance(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 1_000; i++)
            {
                ctx.Client.SimpleUnary(new RequestMessage { Value = 123 });
            }

            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task UnaryAsyncSequentialCallsPerformance(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 1_000; i++)
            {
                ResponseMessage rep = await ctx.Client.SimpleUnaryAsync(new RequestMessage { Value = 123 });
            }

            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task UnaryParallelCallsPerformance(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var stopwatch = Stopwatch.StartNew();
            var tasks = new Task[1_000];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = ctx.Client.SimpleUnaryAsync(new RequestMessage()).ResponseAsync;
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public void UnaryLargePayloadPerformance(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            //var bytes = new byte[100 * 1024 * 1024];
            var bytes = new byte[100 * 1024 * 1024];
            ByteString byteString = ByteString.CopyFrom(bytes);
            var stopwatch = Stopwatch.StartNew();
            ctx.Client.SimpleUnary(new RequestMessage { Binary = byteString });
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public void ChannelColdStartPerformance(ChannelContextFactory factory)
        {
            // Note: This test needs to be run on its own for accurate cold start measurements.
            var stopwatch = Stopwatch.StartNew();
            using var ctx = factory.Create();
            ctx.Client.SimpleUnary(new RequestMessage());
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public void ChannelWarmStartPerformance(ChannelContextFactory factory)
        {
            using var tempChannel = factory.Create();
            var stopwatch = Stopwatch.StartNew();
            using var ctx = factory.Create();
            ctx.Client.SimpleUnary(new RequestMessage());
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }
    }
}