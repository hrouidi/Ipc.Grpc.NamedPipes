using Ipc.Grpc.SharedMemory;

namespace Ipc.Grpc.SharedMemoryVsNamedPipe.Benchmarks.Helpers;

public sealed class SharedMemoryChannel : IDisposable
{
    public MainSharedMemory Client { get; }
    public MainSharedMemory Server { get; }

    private SharedMemoryChannel(MainSharedMemory client, MainSharedMemory server)
    {
        Client = client;
        Server = server;
    }

    public static SharedMemoryChannel Create(string name)
    {
        SharedMemoryChannel ret = new(CreateClientPipe(name), CreateServerPipe(name));
        return ret;
    }

    public static MainSharedMemory CreateClientPipe(string name)
    {
        return new MainSharedMemory(name);
    }

    public static MainSharedMemory CreateServerPipe(string name)
    {
        return new MainSharedMemory(name);
    }

    public static SharedMemoryChannel CreateRandom()
    {
        return Create(Guid.NewGuid().ToString());
    }

    public void Dispose()
    {
        Client.Dispose();
        Server.Dispose();
    }
}