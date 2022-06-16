using System;

using Ipc.Grpc.NamedPipes.Tests.ProtoContract;
using ProtoBuf.Grpc.Client;

namespace Ipc.Grpc.NamedPipes.Tests.Client;

public sealed class RemoteChannel : IDisposable
{
    private readonly RemoteProcessManager _remoteProcess;
    public ITestService Client { get; }

    public RemoteChannel(ITestService client, RemoteProcessManager remoteProcess)
    {
        _remoteProcess = remoteProcess;
        Client = client;
    }

    public void Dispose() => _remoteProcess.Dispose();
}

public static class RemoteChannelFactory
{
    public static ITestService Create(string pipeName)
    {
        var channel = new NamedPipeChannel(pipeName, new NamedPipeChannelOptions { ConnectionTimeout = 100 });
        return channel.CreateGrpcService<ITestService>();
    }

    public static RemoteChannel CreateNamedPipe(string exeFilePath)
    {
        string pipeName = Guid.NewGuid().ToString();
        var channel = new NamedPipeChannel(pipeName, new NamedPipeChannelOptions { ConnectionTimeout = 100 });
        var client = channel.CreateGrpcService<ITestService>();
        return new RemoteChannel(client, new RemoteProcessManager(exeFilePath, pipeName));
    }

}