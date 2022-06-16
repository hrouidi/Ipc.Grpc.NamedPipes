//using System;
//using System.Diagnostics;
//using System.Threading.Tasks;
//using Google.Protobuf;
//using Grpc.Core;
//using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
//using Ipc.Grpc.NamedPipes.VsHttp.Tests.CaseSources;
//using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
//using NUnit.Framework;

//namespace Ipc.Grpc.NamedPipes.VsHttp.Tests.InSameProcess.PerformanceTests
//{
//    public class ServerStreamingTests
//    {
//        public const int TestTimeout = 10 * 1000;

//        [Test, Timeout(TestTimeout)]
//        [TestCaseSource(typeof(MultiChannelSource))]
//        public async Task ServerStreamingManyMessagesPerformance(ChannelContextFactory factory)
//        {
//            using ChannelContext ctx = factory.Create();
//            var stopwatch = Stopwatch.StartNew();
//            var call = ctx.Client.ServerStreaming(new RequestMessage { Value = 10_000 });
//            int cpt = 0;
//            while (await call.ResponseStream.MoveNext())
//            {
//                //Console.WriteLine($"sssssssssssssss {++cpt}");
//            }

//            stopwatch.Stop();
//            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
//        }

//        [Test, Timeout(TestTimeout)]
//        [TestCaseSource(typeof(MultiChannelSource))]
//        public async Task ServerStreamingManyBigMessagesPerformance(ChannelContextFactory factory)
//        {
//            using ChannelContext ctx = factory.Create();
//            var bytes = new byte[10 * 1024 * 1024];
//            ByteString byteString = UnsafeByteOperations.UnsafeWrap(bytes);
//            RequestMessage request = new() { Value = 100, Binary = byteString };
//            var stopwatch = Stopwatch.StartNew();
//            var call = ctx.Client.ServerStreaming(request);
//            while (await call.ResponseStream.MoveNext())
//            {
//            }

//            stopwatch.Stop();
//            Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString());
//        }

//        //TODO : cover more cases

//    }
//}