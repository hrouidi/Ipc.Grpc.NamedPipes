using System;
using System.IO;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using BenchmarkDotNet.Jobs;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Ipc.Grpc.NamedPipes.Benchmarks.Helpers;
using Ipc.Grpc.NamedPipes.Protocol;
using Perfolizer.Mathematics.OutlierDetection;

namespace Ipc.Grpc.NamedPipes.Benchmarks
{
    [Outliers(OutlierMode.DontRemove)]
    [MemoryDiagnoser]
    //[NativeMemoryProfiler]
    //[SimpleJob(runtimeMoniker: RuntimeMoniker.Net461)]
    //[SimpleJob(runtimeMoniker: RuntimeMoniker.Net60)]
    public class WriteToPipe
    {
        private const int Iterations = 10000;
        private ClientMessage _clientMessage;

        private PipeStreamCouple _pipeStreamCouple;


        [GlobalSetup]
        public void GlobalSetup()
        {
            _clientMessage = new ClientMessage
                             {
                Request = new Request
                {
                    MethodFullName = "Method#1",
                    MethodType = Request.Types.MethodType.Unary,
                    Deadline = Timestamp.FromDateTime(DateTime.UtcNow),
                    Headers = new Headers(),
                }
            };
        }

        [IterationSetup]
        public void IterationSetup()
        {
            _pipeStreamCouple = PipeStreamCouple.Create("test");
            var task = ReadLoopAsync(_pipeStreamCouple.ServerStream);

        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            _pipeStreamCouple.Dispose();
        }

        [Benchmark(Baseline = true)]
        public void WriteViaBufferDispose()
        {
            for (int i = 0; i < Iterations; i++)
            {
                using var buffer = new MemoryStream();
                _clientMessage.WriteTo(buffer);
                buffer.WriteTo(_pipeStreamCouple.ClientStream);
            }
            
        }

        [Benchmark]
        public void WriteViaIMessage()
        {
            for (int i = 0; i < Iterations; i++)
            {
                _clientMessage.WriteTo(_pipeStreamCouple.ClientStream);
            }
        }

        private static async Task ReadLoopAsync(NamedPipeServerStream stream)
        {
            while (stream.IsConnected)
            {
                var reader = PipeReader.Create(stream);
                ReadResult read = await reader.ReadAsync();
                //return ClientMessage.Parser.ParseFrom(read.Buffer);
            }
        }
    }
}
