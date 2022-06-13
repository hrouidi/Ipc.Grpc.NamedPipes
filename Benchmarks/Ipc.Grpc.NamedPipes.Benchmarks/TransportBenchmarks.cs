using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Ipc.Grpc.NamedPipes.Benchmarks.Helpers;
using Ipc.Grpc.NamedPipes.Internal;
using Ipc.Grpc.NamedPipes.Internal.Transport;
using Perfolizer.Mathematics.OutlierDetection;
using Message = Ipc.Grpc.NamedPipes.Internal.Transport.Message;

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
        private NamedPipeTransport _clientTransporter;
        private NamedPipeTransport _serverTransporter;


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

            _clientTransporter = new NamedPipeTransport(_channel.ClientStream);
            _serverTransporter = new NamedPipeTransport(_channel.ServerStream);

        }


        [IterationCleanup]
        public void IterationCleanup()
        {
            _channel.Dispose();
            _clientTransporter.Dispose();
            _serverTransporter.Dispose();
        }

        [Benchmark(Baseline = true)]
        public async Task<int> SendFrame3()
        {
            int i = 0;
            for (; i < Iterations; i++)
            {
                var task = _serverTransporter.ReadFrame();
                await _clientTransporter.SendFrame(_expectedRequest);
                await task;
            }
            return i;
        }
    }
}
