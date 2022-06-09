using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Ipc.Grpc.NamedPipes.VsBenchmarks
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //Summary summary = BenchmarkRunner.Run<FrameHeaderBenchmarks>();
            Summary summary = BenchmarkRunner.Run<UnaryAsyncBenchmarks>();
        }
    }
}
