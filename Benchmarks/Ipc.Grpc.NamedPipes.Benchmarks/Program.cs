using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Ipc.Grpc.NamedPipes.Benchmarks
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Summary summary = BenchmarkRunner.Run<TransportBenchmarks>();
        }
    }
}
