using System;
using System.Collections.Generic;
using System.Linq;
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
        public const int Iterations = 4 * 1_000;

        private PipeChannel _channel;
        private PipeChannel _channel2;

        private List<byte[]> _expectedRequestPayloads;
        private List<Message> _expectedRequests;

        private NamedPipeTransport _clientTransporter;
        private NamedPipeTransport _serverTransporter;

        private NamedPipeTransport2 _clientTransporter2;
        private NamedPipeTransport2 _serverTransporter2;


        [GlobalSetup]
        public void GlobalSetup()
        {
        }

        [IterationSetup]
        public void IterationSetup()
        {
            _channel = PipeChannel.CreateRandom();
            _channel2 = PipeChannel.CreateRandom();

            _expectedRequests = Enumerable.Range(0, Iterations)
                                          .Select(x => new Message())
                                          .ToList();
            Random random = new();
            _expectedRequestPayloads = Enumerable.Range(0, Iterations)
                                                 .Select(x =>
                                                 {
                                                     var ret = new byte[100];
                                                     random.NextBytes(ret);
                                                     return ret;
                                                 })
                                                 .ToList();


            _clientTransporter = new NamedPipeTransport(_channel.ClientStream);
            _clientTransporter2 = new NamedPipeTransport2(_channel.ClientStream);
            _serverTransporter = new NamedPipeTransport(_channel.ServerStream);
            _serverTransporter2 = new NamedPipeTransport2(_channel.ServerStream);

        }


        [IterationCleanup]
        public void IterationCleanup()
        {
            _channel.Dispose();
            _channel2.Dispose();
            _clientTransporter.Dispose();
            _clientTransporter2.Dispose();
            _serverTransporter.Dispose();
            _serverTransporter2.Dispose();
        }

        [Benchmark(Baseline = true)]
        public async Task<List<Message>> SendFrame()
        {
            return await Run(_clientTransporter, _serverTransporter);
        }

        [Benchmark]
        public async Task<List<Message>> SendFrame2()
        {
            return await Run(_clientTransporter2, _serverTransporter2);
        }

        private async Task<List<Message>> Run(INamedPipeTransport client, INamedPipeTransport server)
        {
            Task writeTask = Task.Factory.StartNew(async () =>
            {
                foreach (Message message in _expectedRequests)
                    await client.SendFrame(message);
            }, TaskCreationOptions.LongRunning).Unwrap();

            Task<List<Message>> readTask = Task.Factory.StartNew(async () =>
            {
                List<Message> actualMessages = new(_expectedRequests.Count);
                for (int i = 0; i < _expectedRequests.Count; i++)
                    actualMessages.Add(await server.ReadFrame());
                return actualMessages;
            }, TaskCreationOptions.LongRunning).Unwrap();
            await Task.WhenAll(writeTask, readTask);
            return readTask.Result;
        }
    }
}
