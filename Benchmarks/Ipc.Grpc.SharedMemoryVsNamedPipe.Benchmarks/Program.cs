using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Ipc.Grpc.SharedMemoryVsNamedPipe.Benchmarks
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //Summary summary = BenchmarkRunner.Run<SyncNamedPipeVsSharedMemory>();
            Summary summary = BenchmarkRunner.Run<NamedPipeVsSharedMemory>();
           // Summary summary = BenchmarkRunner.Run<SharedMemory>();
        }
    }
}
