using Ipc.Grpc.SharedMemory;

namespace Ipc.Grpc.SharedMemoryVsNamedPipe.Benchmarks.Helpers;

public sealed class SharedMemoryChannel2 : IDisposable
{
    public MainSharedMemory2 Client { get; }
    public MainSharedMemory2 Server { get; }

    private SharedMemoryChannel2(MainSharedMemory2 client, MainSharedMemory2 server)
    {
        Client = client;
        Server = server;
    }

    public static SharedMemoryChannel2 Create(string name)
    {
        SharedMemoryChannel2 ret = new(CreateClientPipe(name), CreateServerPipe(name));
        return ret;
    }

    public static MainSharedMemory2 CreateClientPipe(string name)
    {
        return new MainSharedMemory2(name);
    }

    public static MainSharedMemory2 CreateServerPipe(string name)
    {
        return new MainSharedMemory2(name);
    }

    public static SharedMemoryChannel2 CreateRandom()
    {
        return Create(Guid.NewGuid().ToString());
    }

    public void Dispose()
    {
        Client.Dispose();
        Server.Dispose();
    }
}