using System;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.Tests.ProtoContract;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers
{
    public class GrpcDotNetNamedPipesChannelFactory : ChannelContextFactory
    {
        private readonly string _pipeName = $"GrpcNamedPipeTests/{Guid.NewGuid()}";
        private const int _connectionTimeout = 100;

        public ChannelContext Create(GrpcDotNetNamedPipes.NamedPipeServerOptions options)
        {

            var impl = new TestServiceImplementation();
            var server = new GrpcDotNetNamedPipes.NamedPipeServer(_pipeName, options);
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
            return Create(new GrpcDotNetNamedPipes.NamedPipeServerOptions());
        }

        public override TestService.TestServiceClient CreateClient()
        {
            var channel = new GrpcDotNetNamedPipes.NamedPipeChannel(".",_pipeName, new GrpcDotNetNamedPipes.NamedPipeChannelOptions { ConnectionTimeout = _connectionTimeout });
            return new TestService.TestServiceClient(channel);
        }

        public GrpcDotNetNamedPipes.NamedPipeServer CreateServer() => new(_pipeName);

        public override string ToString()
        {
            return "GrpcDotNetNamedPipes";
        }
    }
}