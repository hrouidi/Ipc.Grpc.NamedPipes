using System;
using System.Threading.Tasks;
using Ipc.Grpc.NamedPipes.Tests.ProtoContract;


namespace Ipc.Grpc.NamedPipes.Tests.Server;

public class Program
{
    public static async Task Main(string[] args)
    {
        string pipeName = GetPipeName(args);
        var impl = new TestServiceImplementation();
        var server = new NamedPipeServer(pipeName);
        server.Bind<ITestService>(impl);
        Console.WriteLine($"Server started at :{pipeName}");
        await server.RunAsync();
    }

    private static string GetPipeName(string[] args)
    {
        if (args.Length == 0)
            return $"Random/{Guid.NewGuid()}";
        return args[0];
    }
}