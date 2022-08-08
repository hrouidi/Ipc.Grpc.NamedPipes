using System.IO.Pipes;
using Ipc.Grpc.SharedMemory;
using PipeOptions = System.IO.Pipes.PipeOptions;

namespace Ipc.Grpc.SharedMemoryVsNamedPipe.Benchmarks.Helpers;

public sealed class PipeChannel : IDisposable
{
    public MainPipe Client { get; }
    public MainPipe Server { get; }

    private PipeChannel(MainPipe client, MainPipe server)
    {
        Client = client;
        Server = server;
    }

    public static PipeChannel Create(string pipeName)
    {
        var client = CreateClientPipe(pipeName);
        var server = CreateServerPipe(pipeName);
        Task.WaitAll(client.ConnectAsync(), server.WaitForConnectionAsync());
        client.ReadMode = PipeTransmissionMode.Message;
        PipeChannel ret = new(new MainPipe(client), new MainPipe(server));
        return ret;
    }

    public static NamedPipeClientStream CreateClientPipe(string name)
    {
        return new NamedPipeClientStream(".",
            name,
            PipeDirection.InOut,
            PipeOptions.Asynchronous,
            System.Security.Principal.TokenImpersonationLevel.None,
            HandleInheritability.None);
    }

    public static NamedPipeServerStream CreateServerPipe(string name)
    {
        return new NamedPipeServerStream(name,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous, 0, 0);
    }

    public static PipeChannel CreateRandom()
    {
        return Create(Guid.NewGuid().ToString());
    }

    public void Dispose()
    {
        Client.Dispose();
        Server.Dispose();
    }
}