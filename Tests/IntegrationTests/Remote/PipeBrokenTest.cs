using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Google.Protobuf;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.Tests.Client;
using NUnit.Framework;

namespace Ipc.Grpc.NamedPipes.Remote.Tests
{
    public class PipeBrokenTest
    {
        public const int TestTimeout = 10 * 1000;
        public readonly string ExeFile;

        public PipeBrokenTest()
        {
            var tmp = Directory.GetCurrentDirectory();
            ExeFile = Path.Combine(Directory.GetCurrentDirectory(), @"../../../../Ipc.Grpc.NamedPipes.Tests.Server/bin/Debug/Ipc.Grpc.NamedPipes.Tests.Server.exe");
            var ret = File.Exists(ExeFile);
        }

        [Test]
        //[TestCase(1)]
        [TestCase(10_000)]
        public async Task DebugBrokenPipeIssue(int iterationCpt)
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
    }
}