using System;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;

namespace Ipc.Grpc.NamedPipes.Tests.Client;

public sealed class RemoteChannel : IDisposable
{
    private readonly RemoteProcessManager _remoteProcess;
    public TestService.TestServiceClient Client { get; }

    public RemoteChannel(TestService.TestServiceClient client, RemoteProcessManager remoteProcess)
    {
        _remoteProcess = remoteProcess;
        Client = client;
    }

    public void Dispose() => _remoteProcess.Dispose();
}

public static class RemoteChannelFactory
{
    public static TestService.TestServiceClient Create(string pipeName)
    {
        var channel = new NamedPipeChannel(pipeName, new NamedPipeChannelOptions { ConnectionTimeout = 100 });
        return new TestService.TestServiceClient(channel);
    }

    public static RemoteChannel CreateNamedPipe(string exeFilePath)
    {
        string pipeName = Guid.NewGuid().ToString();
        var channel = new NamedPipeChannel(pipeName, new NamedPipeChannelOptions { ConnectionTimeout = 100 });
        var client = new TestService.TestServiceClient(channel);
        return new  RemoteChannel(client, new RemoteProcessManager(exeFilePath, pipeName));
    }

}