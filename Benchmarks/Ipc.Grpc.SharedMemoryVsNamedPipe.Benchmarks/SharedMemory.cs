﻿using BenchmarkDotNet.Attributes;
using Ipc.Grpc.SharedMemory;
using Ipc.Grpc.SharedMemoryVsNamedPipe.Benchmarks.Helpers;
using Perfolizer.Mathematics.OutlierDetection;

namespace Ipc.Grpc.SharedMemoryVsNamedPipe.Benchmarks
{
    [Outliers(OutlierMode.DontRemove)]
    [MemoryDiagnoser]
    public class SharedMemory
    {
        public const int Iterations = 10_000;

        //private PipeChannel _pipeChannel;
        //private SharedMemoryChannel _sharedMemoryChannel;
        private List<(Guid guid, int size)> _expectedMessages;


        [IterationSetup]
        public void IterationSetup()
        {
            _expectedMessages = Enumerable.Range(0, Iterations)
                                          .Select(x => (guid: Guid.NewGuid(), size: x))
                                          .ToList();
        }


        [Benchmark(Baseline = true)]
        public async Task<List<(Guid, int)>> ReadWrite_SharedMemory()
        {
            var channel = SharedMemoryChannel.CreateRandom();
            var ret = await RunSync(channel.Client, channel.Server);
            channel.Dispose();
            return ret;
        }
        [Benchmark]
        public async Task<List<(Guid, int)>> ReadWrite_SharedMemory2()
        {
            var channel = SharedMemoryChannel2.CreateRandom();
            var ret = await RunSync(channel.Client, channel.Server);
            channel.Dispose();
            return ret;
        }



        private async Task<List<(Guid, int)>> Run(IMainMemory clientMemory, IMainMemory serverMemory)
        {
            Task writeTask = Task.Factory.StartNew(async () =>
            {
                foreach ((Guid guid, int size) in _expectedMessages)
                    await clientMemory.WriteAsync(guid, size);
            }, TaskCreationOptions.LongRunning).Unwrap();

            Task<List<(Guid, int)>> readTask = Task.Factory.StartNew(async () =>
            {
                List<(Guid, int)> actualMessages = new(_expectedMessages.Count);
                for (int i = 0; i < _expectedMessages.Count; i++)
                    actualMessages.Add(await serverMemory.ReadAsync());
                return actualMessages;
            }, TaskCreationOptions.LongRunning).Unwrap();
            await Task.WhenAll(writeTask, readTask);
            return readTask.Result;
        }

        private async Task<List<(Guid, int)>> RunSync(IMainMemory clientMemory, IMainMemory serverMemory)
        {
            Task writeTask = Task.Factory.StartNew(() =>
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
