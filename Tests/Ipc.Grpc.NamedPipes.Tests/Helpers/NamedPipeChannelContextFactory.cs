
using System;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.Tests.ProtoContract;

namespace Ipc.Grpc.NamedPipes.Tests.Helpers
{
    public class NamedPipeChannelContextFactory : ChannelContextFactory
    {
        private readonly string _pipeName = $"Ipc.Grpc.NamedPipes/{Guid.NewGuid()}";

        private const int _connectionTimeout = 100;
        //private const int _connectionTimeout = 100;
            
        public ChannelContext Create(NamedPipeServerOptions options)
        {
            
            var impl = new TestServiceImplementation();
            var server = new NamedPipeServer(_pipeName, options);
            TestService.BindService(server.ServiceBinder, impl);
            server.Start();
            return new ChannelContext
            {
                Impl = impl,
                Client = CreateClient(),
                OnDispose = () => server.Kill()
            };
        }

        public override ChannelContext Create()
        {
            return Create(new NamedPipeServerOptions());
        }

        public override TestService.TestServiceClient CreateClient()
        {
            var channel = new NamedPipeChannel( _pipeName, new NamedPipeChannelOptions { ConnectionTimeout = _connectionTimeout });
            return new TestService.TestServiceClient(channel);
        }

        public NamedPipeServer CreateServer() => new(_pipeName);

        public override string ToString()
        {
            return "Ipc.Grpc.NamedPipes";
        }
    }
}