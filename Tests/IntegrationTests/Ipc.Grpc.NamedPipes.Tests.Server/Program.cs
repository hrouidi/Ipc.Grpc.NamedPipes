using System;
using System.Threading.Tasks;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.Tests.ProtoContract;

namespace Ipc.Grpc.NamedPipes.Tests.Server;

public class Program
{
    public static async Task Main(string[] args)
    {
        string pipeName = GetPipeName(args);
        var impl = new TestServiceImplementation();

        var server = new NamedPipeServer(pipeName);
        
        IdleTimeInterceptor idleTimeInterceptor = new(TimeSpan.FromMinutes(5), () => server.Shutdown());
        server.AddInterceptor(idleTimeInterceptor);

        TestService.BindService(server.ServiceBinder, impl);

        Console.WriteLine($"Server starting at :{pipeName}");
        idleTimeInterceptor.Start();
        await server.RunAsync();
        Console.WriteLine($"Server shutdown at :{pipeName}");
    }

    private static string GetPipeName(string[] args)
    {
        if (args.Length == 0)
            return $"Random/{Guid.NewGuid()}";
        return args[0];
    }
}