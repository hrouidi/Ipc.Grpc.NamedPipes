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
    public class TransportBenchmarks
    {
        public const int Iterations = 2*1000;

        private PipeChannel _channel;
        private byte[] _expectedRequestPayload;
        private Frame _expectedRequest;
        private NamedPipeTransportV2 _clientTransport;
        private NamedPipeTransportV2 _serverTransport;


        [GlobalSetup]
        public void GlobalSetup()
        {
        }

        [IterationSetup]
        public void IterationSetup()
        {
            _channel = PipeChannel.CreateRandom();

            _expectedRequest = new Frame();

            Random random = new();
            _expectedRequestPayload = new byte[100];
            random.NextBytes(_expectedRequestPayload);

            _clientTransport = new NamedPipeTransportV2(_channel.ClientStream);
            _serverTransport = new NamedPipeTransportV2(_channel.ServerStream);

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
                var task = _serverTransport.ReadFrame3();
                await _clientTransport.SendFrame3(_expectedRequest, SerializeRequestPayload3);
                await task;
            }
            return i;
        }

        [Benchmark]
        public async Task<int> SendFrame2()
        {
            int i = 0;
            for (; i < Iterations; i++)
            {
                var task = _serverTransport.ReadFrame();
                await _clientTransport.SendFrame2(_expectedRequest, SerializeRequestPayload2);
                await task;
            }
            return i;
        }

        [Benchmark]
        public async Task<int> SendFrame()
        {
            int i = 0;
            for (; i < Iterations; i++)
            {
                var task = _serverTransport.ReadFrame();
                await _clientTransport.SendFrame(_expectedRequest, SerializeRequestPayload1);
                await task;
            }
            return i;
        }


        private (Memory<byte> MsgBytes, int frameSize, int payloadSize) SerializeRequestPayload3(Frame frame)
        {
            int padding = NamedPipeTransportV2.FrameHeader.Size;
            int frameSize = frame.CalculateSize();
            //All
            var owner = MemoryPool<byte>.Shared.Rent(padding + frameSize + _expectedRequestPayload.Length);
            Memory<byte> messageBytes = owner.Memory.Slice(0, padding + frameSize + _expectedRequestPayload.Length);
            //#1 : will be set later in send method

            //#2 : frame
            Memory<byte> frameBytes = messageBytes.Slice(padding, frameSize);
            frame.WriteTo(frameBytes.Span);
            //#3 : payload
            Memory<byte> payLoadBytes = messageBytes.Slice(padding + frameSize);
            _expectedRequestPayload.AsMemory()
                                   .CopyTo(payLoadBytes);

            return (messageBytes, frameSize, _expectedRequestPayload.Length);
        }

        private (Memory<byte>, int) SerializeRequestPayload2(Frame frame)
        {
            int frameSize = frame.CalculateSize();
            var owner = MemoryPool<byte>.Shared.Rent(frameSize + _expectedRequestPayload.Length);
            Memory<byte> messageBytes = owner.Memory.Slice(0, frameSize + _expectedRequestPayload.Length);
            frame.WriteTo(messageBytes.Span.Slice(0, frameSize));

            Memory<byte> payLoadBytes = messageBytes.Slice(frameSize);
            _expectedRequestPayload.AsMemory()
                                   .CopyTo(payLoadBytes);

            return (messageBytes, _expectedRequestPayload.Length);
        }

        private void SerializeRequestPayload1(MemoryStream stream) => stream.Write(_expectedRequestPayload, 0, 100);
    }
}
