using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Google.Protobuf;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
using Perfolizer.Mathematics.OutlierDetection;

namespace Ipc.Grpc.NamedPipes.VsBenchmarks
{
    [Outliers(OutlierMode.DontRemove)]
    [SimpleJob(RuntimeMoniker.Net48)]
    [MemoryDiagnoser]
    public class UnaryAsyncBenchmarks
    {
        public const int Iterations = 2 * 1000;

        private ChannelContext _ctx1;
        private ChannelContext _ctx2;
        private ChannelContext _ctx3;

        private NamedPipeChannelContextFactory _factory1;
        private GrpcDotNetNamedPipesChannelFactory _factory2;
        private HttpChannelContextFactory _factory3;

        private ByteString _byteString;

        private RequestMessage _requestMessage;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _factory1 = new NamedPipeChannelContextFactory();
            _factory2 = new GrpcDotNetNamedPipesChannelFactory();
            _factory3 = new HttpChannelContextFactory();
            Random random = new();
            var bytes = new byte[100 * 1024 * 1024];
            random.NextBytes(bytes);
            _byteString = UnsafeByteOperations.UnsafeWrap(bytes);
            _requestMessage = new RequestMessage { Binary = _byteString };
            IterationSetup();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            IterationCleanup();
        }

        //[IterationSetup]
        public void IterationSetup()
        {
            _ctx1 = _factory1.Create();
            _ctx2 = _factory2.Create();
            _ctx3 = _factory3.Create();
        }


        //[IterationCleanup]
        public void IterationCleanup()
        {
            _ctx1?.Dispose();
            _ctx2?.Dispose();
            _ctx3?.Dispose();
        }

        [Benchmark(Baseline = true)]
        public Task<ResponseMessage> LargePayloadPerformance_New() => Run(_ctx1, _requestMessage);


        [Benchmark]
        public Task<ResponseMessage> LargePayloadPerformance_Ben() => Run(_ctx2, _requestMessage);


        [Benchmark]
        public Task<ResponseMessage> LargePayloadPerformance_Http() => Run(_ctx3, _requestMessage);


        private static async Task<ResponseMessage> Run(ChannelContext channelContext, RequestMessage request)
        {
            return await channelContext.Client.SimpleUnaryAsync(request);
        }
    }
}
