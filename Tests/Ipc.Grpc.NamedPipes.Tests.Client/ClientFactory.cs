using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;

namespace Ipc.Grpc.NamedPipes.Tests.Client;

public static class ClientFactory 
{
    public static TestService.TestServiceClient Create(string pipeName)
    {
        var channel = new NamedPipeChannel(pipeName, new NamedPipeChannelOptions { ConnectionTimeout = 1000 });
        return new TestService.TestServiceClient(channel);
    }
    
}