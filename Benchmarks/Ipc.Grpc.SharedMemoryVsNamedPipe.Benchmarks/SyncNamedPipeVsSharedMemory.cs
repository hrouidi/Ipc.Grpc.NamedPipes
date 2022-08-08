using BenchmarkDotNet.Attributes;
using Ipc.Grpc.SharedMemory;
using Ipc.Grpc.SharedMemoryVsNamedPipe.Benchmarks.Helpers;
using Perfolizer.Mathematics.OutlierDetection;

namespace Ipc.Grpc.SharedMemoryVsNamedPipe.Benchmarks
{
    [Outliers(OutlierMode.DontRemove)]
    [MemoryDiagnoser]
    public class SyncNamedPipeVsSharedMemory
    {
        public const int Iterations = 10_000;

        //private PipeChannel _pipeChannel;
        //private SharedMemoryChannel _sharedMemoryChannel;
        private List<(Guid guid, int size)> _expectedMessages;


        [GlobalSetup]
        public void GlobalSetup()
        {
        }

        [IterationSetup]
        public void IterationSetup()
        {
            //_pipeChannel = PipeChannel.CreateRandom();
            //_sharedMemoryChannel = SharedMemoryChannel.CreateRandom();

            _expectedMessages = Enumerable.Range(0, Iterations)
                                          .Select(x => (guid: Guid.NewGuid(), size: x))
                                          .ToList();
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            //_pipeChannel.Dispose();
            //_sharedMemoryChannel.Dispose();
        }

        [Benchmark(Baseline = true)]
        public async Task<List<(Guid, int)>> ReadWrite_SharedMemory()
        {
            var channel = SharedMemoryChannel.CreateRandom();
            var ret = await Run(channel.Client, channel.Server);
            channel.Dispose();
            return ret;
        }

        [Benchmark]
        public async Task<List<(Guid, int)>> ReadWrite_NamedPipe()
        {
            var channel = PipeChannel.CreateRandom();
            var ret = await Run(channel.Client, channel.Server);
            channel.Dispose();
            return ret;
        }

        private async Task<List<(Guid, int)>> Run(IMainMemory clientMemory, IMainMemory serverMemory)
        {
            Task writeTask = Task.Factory.StartNew( () =>
            {
                foreach ((Guid guid, int size) in _expectedMessages)
                    clientMemory.WriteSync(guid, size);
            }, TaskCreationOptions.LongRunning);

            Task<List<(Guid, int)>> readTask = Task.Factory.StartNew(() =>
            {
                List<(Guid, int)> actualMessages = new(_expectedMessages.Count);
                for (int i = 0; i < _expectedMessages.Count; i++)
                    actualMessages.Add(serverMemory.ReadSync());
                return actualMessages;
            }, TaskCreationOptions.LongRunning);
            await Task.WhenAll(writeTask, readTask);
            return readTask.Result;
        }
    }
}
