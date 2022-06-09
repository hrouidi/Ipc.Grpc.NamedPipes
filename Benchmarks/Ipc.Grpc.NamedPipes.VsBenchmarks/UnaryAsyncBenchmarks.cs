using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Google.Protobuf;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
using Perfolizer.Mathematics.OutlierDetection;

namespace Ipc.Grpc.NamedPipes.VsBenchmarks
{
    [Outliers(OutlierMode.DontRemove)]
    [MemoryDiagnoser]
    public class UnaryAsyncBenchmarks
    {
        public const int Iterations = 2 * 1000;

        private ChannelContext ctx1;
        private ChannelContext ctx2;
        private ChannelContext ctx3;

        private NamedPipeChannelContextFactory factory1;
        private GrpcDotNetNamedPipesChannelFactory factory2;
        private HttpChannelContextFactory factory3;

        private ByteString byteString;

        private RequestMessage requestMessage;

        [GlobalSetup]
        public void GlobalSetup()
        {
            factory1 = new NamedPipeChannelContextFactory();
            factory2 = new GrpcDotNetNamedPipesChannelFactory();
            factory3 = new HttpChannelContextFactory();

            var bytes = new byte[100 * 1024 * 1024];
            byteString = ByteString.CopyFrom(bytes);
            requestMessage = new RequestMessage { Binary = byteString };
        }


        [IterationSetup]
        public void IterationSetup()
        {
            ctx1 = factory1.Create();
            ctx2 = factory2.Create();
            ctx3 = factory3.Create();
        }


        [IterationCleanup]
        public void IterationCleanup()
        {
            ctx1?.Dispose();
            ctx2?.Dispose();
            ctx3?.Dispose();
        }


        [Benchmark(Baseline = true)]
        public async Task<ResponseMessage> LargePayloadPerformance_New()
        {
            //ctx1 = factory1.Create();
            ResponseMessage ret = null;
            //for (int i = 0; i < 10; i++)
            ret = await ctx1.Client.SimpleUnaryAsync(requestMessage);
            return ret;
        }

        [Benchmark]
        public async Task<ResponseMessage> LargePayloadPerformance_Ben()
        {
            ctx2 = factory2.Create();
            ResponseMessage ret = null;
            //for (int i = 0; i < 10; i++)
            ret = await ctx2.Client.SimpleUnaryAsync(requestMessage);
            return ret;
        }
        [Benchmark]
        public async Task<ResponseMessage> LargePayloadPerformance_Http()
        {
            ctx3 = factory3.Create();
            ResponseMessage ret = null;
            //for (int i = 0; i < 10; i++)
            ret = await ctx3.Client.SimpleUnaryAsync(requestMessage);
            return ret;
        }
    }
}
