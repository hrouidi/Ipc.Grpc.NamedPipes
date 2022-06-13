using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Ipc.Grpc.NamedPipes.Benchmarks.Helpers;
using Ipc.Grpc.NamedPipes.Internal;
using Ipc.Grpc.NamedPipes.TransportProtocol;
using Perfolizer.Mathematics.OutlierDetection;

namespace Ipc.Grpc.NamedPipes.Benchmarks
{
    [Outliers(OutlierMode.DontRemove)]
    [MemoryDiagnoser]
    //[NativeMemoryProfiler]
    //[SimpleJob(runtimeMoniker: RuntimeMoniker.Net461)]
    //[SimpleJob(runtimeMoniker: RuntimeMoniker.Net60)]
    public class TransportBenchmarks
    {
        public const int Iterations = 2*1000;

        private PipeChannel _channel;
        private byte[] _expectedRequestPayload;
        private Message _expectedRequest;
        private Transport _clientTransport;
        private Transport _serverTransport;


        [GlobalSetup]
        public void GlobalSetup()
        {
        }

        [IterationSetup]
        public void IterationSetup()
        {
            _channel = PipeChannel.CreateRandom();

            _expectedRequest = new Message();

            Random random = new();
            _expectedRequestPayload = new byte[100];
            random.NextBytes(_expectedRequestPayload);

            _clientTransport = new Transport(_channel.ClientStream);
            _serverTransport = new Transport(_channel.ServerStream);

        }


        [IterationCleanup]
        public void IterationCleanup()
        {
            _channel.Dispose();
            _clientTransport.Dispose();
            _serverTransport.Dispose();
        }

        [Benchmark(Baseline = true)]
        public async Task<int> SendFrame3()
        {
            int i = 0;
            for (; i < Iterations; i++)
            {
                var task = _serverTransport.ReadFrame();
                await _clientTransport.SendFrame(_expectedRequest);
                await task;
            }
            return i;
        }
    }
}
