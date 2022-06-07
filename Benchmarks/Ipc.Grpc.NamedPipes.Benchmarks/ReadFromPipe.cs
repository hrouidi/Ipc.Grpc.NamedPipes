using System;
using System.IO;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Protocol;
using Perfolizer.Mathematics.OutlierDetection;

namespace Ipc.Grpc.NamedPipes.Benchmarks
{
    [Outliers(OutlierMode.DontRemove)]
    [MemoryDiagnoser]
    [SimpleJob(runtimeMoniker: RuntimeMoniker.Net461)]
    //[SimpleJob(runtimeMoniker: RuntimeMoniker.Net60)]
    public class ReadFromPipe
    {
        private const int MessageBufferSize = 16 * 1024; // 16 kiB

        private readonly byte[] _messageBuffer = new byte[MessageBufferSize];

        private const int Iterations = 1000;
        private ClientMessage _clientMessage;

        private PipeStreamCouple _pipeStreamCouple;


        [GlobalSetup]
        public void GlobalSetup()
        {
            var header = new Headers();
            header.Metadata.Add(new MetadataEntry
                                {
                                    ValueBytes = ByteString.CopyFrom(new byte[1024 * 1024])
                                });
                         
            _clientMessage = new ClientMessage
                             {
                Request = new Request
                {
                    MethodFullName = "Method#1",
                    MethodType = Request.Types.MethodType.Unary,
                    Deadline = Timestamp.FromDateTime(DateTime.UtcNow),
                    Headers = header,
                }
            };
        }

        [IterationSetup]
        public void IterationSetup()
        {
            _pipeStreamCouple = PipeStreamCouple.Create("test");
            var task = Task.Run(() => _clientMessage.WriteTo(_pipeStreamCouple.ClientStream));

        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            _pipeStreamCouple.Dispose();
        }


        [Benchmark(Baseline = true)]
        public async Task<ClientMessage> ReadPipeReader()
        {
            var reader = PipeReader.Create(_pipeStreamCouple.ServerStream);
            ReadResult read = await reader.ReadAsync();
            return ClientMessage.Parser.ParseFrom(read.Buffer);
        }

        [Benchmark]
        public async Task<ClientMessage> ReadUsingStreamMemory()
        {
            var packet = new MemoryStream();
            do
            {
                int readBytes = await _pipeStreamCouple.ServerStream.ReadAsync(_messageBuffer, 0, MessageBufferSize).ConfigureAwait(false);
                packet.Write(_messageBuffer, 0, readBytes);
            } while (!_pipeStreamCouple.ServerStream.IsMessageComplete);

            packet.Position = 0;
            return ClientMessage.Parser.ParseFrom(packet);
        }
    }
}
