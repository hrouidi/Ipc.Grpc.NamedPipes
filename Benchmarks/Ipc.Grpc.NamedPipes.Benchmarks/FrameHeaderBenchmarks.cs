using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Google.Protobuf;
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
    public class FrameHeaderBenchmarks
    {
        public const int Iterations = 100* 1_000_000;

        private byte[] _expectedRequestPayload;
        private Random _random;

        private int size1;
        private int size2;


        [GlobalSetup]
        public void GlobalSetup()
        {
            _random = new();
        }

        [IterationSetup]
        public void IterationSetup()
        {
            size1 = _random.Next(0, int.MaxValue);
            size2 = _random.Next(0, int.MaxValue);
        }


        [IterationCleanup]
        public void IterationCleanup()
        {

        }

        [Benchmark(Baseline = true)]
        public int ToSpan()
        {
            int i = 0;
            Span<byte> bytes = stackalloc byte[8];
            for (; i < Iterations; i++)
            {
                NamedPipeTransportV2.FrameHeader h = new(size1<<1, size2>>1);
                NamedPipeTransportV2.FrameHeader.ToSpan(bytes, ref h);
            }
            return i;
        }

        [Benchmark]
        public int ToSpan2()
        {
            int i = 0;
            Span<byte> bytes = stackalloc byte[8];
            for (; i < Iterations; i++)
            {
                NamedPipeTransportV2.FrameHeader.ToSpan2(bytes, size1<<1, size2>>1);
            }
            return i;
        }

        [Benchmark]
        public int ToSpan3()
        {
            int i = 0;
            Span<byte> bytes = stackalloc byte[8];
            for (; i < Iterations; i++)
            {
                NamedPipeTransportV2.FrameHeader.ToSpan3(bytes, size1 << 1, size2 >> 1);
            }
            return i;
        }
    }
}
