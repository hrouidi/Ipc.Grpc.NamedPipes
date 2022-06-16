using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Google.Protobuf;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.Tests.Client;
using NUnit.Framework;

namespace Ipc.Grpc.NamedPipes.Remote.Tests
{
    public class UnaryAsyncTest
    {
        public const int TestTimeout = 10 * 1000;
        public const string ExeFile = @"Ipc.Grpc.NamedPipes.Tests.Server.exe";

        [Test, Timeout(TestTimeout)]
        [TestCase(1_000)]
        public async Task Channels_Sequential_Performance(int iterationCpt)
        {
            string pipeName = Guid.NewGuid().ToString();

            using var manager = new RemoteProcessManager(ExeFile, pipeName);

            List<ResponseMessage> responses = new List<ResponseMessage>(iterationCpt);
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < responses.Capacity; i++)
            {
                TestService.TestServiceClient client = RemoteChannelFactory.Create(pipeName);
                ResponseMessage ret = await client.SimpleUnaryAsync(new RequestMessage { Value = 111 });
                responses.Add(ret);
            }

            stopwatch.Stop();

            Assert.That(responses.Count, Is.EqualTo(iterationCpt));
            foreach (ResponseMessage response in responses)
                Assert.That(response.Value, Is.EqualTo(111));
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        [Test]
        [TestCase(1_000)]
        public async Task Calls_Sequential_Performance(int iterationCpt)
        {
            using RemoteChannel remoteChannel = RemoteChannelFactory.CreateNamedPipe(ExeFile);

            List<ResponseMessage> responses = new(iterationCpt);
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < responses.Capacity; i++)
            {
                ResponseMessage rep = await remoteChannel.Client.SimpleUnaryAsync(new RequestMessage { Value = i });
                responses.Add(rep);
            }

            stopwatch.Stop();
            Assert.That(responses.Count, Is.EqualTo(iterationCpt));
            for (int i = 0; i < responses.Count; i++)
                Assert.That(responses[i].Value, Is.EqualTo(i));
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        [Test, Timeout(TestTimeout)]
        [TestCase(1_000)]
        public async Task Channels_Parallel_Performance(int iterationCpt)
        {
            string pipeName = Guid.NewGuid().ToString();
            using var manager = new RemoteProcessManager(ExeFile, pipeName);
            var stopwatch = Stopwatch.StartNew();
            var tasks = new Task[1_000];
            for (int i = 0; i < tasks.Length; i++)
            {
                TestService.TestServiceClient client = RemoteChannelFactory.Create(pipeName);
                tasks[i] = client.SimpleUnaryAsync(new RequestMessage()).ResponseAsync;
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        [Test, Timeout(TestTimeout)]
        [TestCase(1_000)]
        public async Task Calls_Parallel_Performance(int iterationCpt)
        {
            using RemoteChannel remoteChannel = RemoteChannelFactory.CreateNamedPipe(ExeFile);
            var stopwatch = Stopwatch.StartNew();
            var tasks = new Task[1_000];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = remoteChannel.Client.SimpleUnaryAsync(new RequestMessage()).ResponseAsync;
            }
            await Task.WhenAll(tasks);
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
        }

        [Test, Timeout(TestTimeout)]
        [TestCase(1 * 1024 * 1024)]
        [TestCase(100 * 1024 * 1024)]
        [TestCase(300 * 1024 * 1024)]
        [TestCase(1024 * 1024 * 1024)]
        [TestCase(2024 * 1024 * 1024)]
        public async Task LargePayloadPerformance(int payloadSize)
        {
            using RemoteChannel remoteChannel = RemoteChannelFactory.CreateNamedPipe(ExeFile);

            var bytes = new byte[payloadSize];
            ByteString byteString = UnsafeByteOperations.UnsafeWrap(bytes);
            ResponseMessage ret = null;
            var stopwatch = Stopwatch.StartNew();
            //for (int i = 0; i < 1000; i++)
            ret = await remoteChannel.Client.SimpleUnaryAsync(new RequestMessage { Binary = byteString });
            stopwatch.Stop();
            Assert.That(ret.Binary, Is.EqualTo(byteString));
            Console.WriteLine($" Elapsed :{stopwatch.Elapsed}");
        }
    }
}