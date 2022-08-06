using System;
using Ipc.Grpc.NamedPipes.Tests.ProtoContract;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;

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
            server.ServiceBinder.AddCodeFirst(impl);
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

        public override ITestService CreateClient()
        {
            var channel = new GrpcDotNetNamedPipes.NamedPipeChannel(".", _pipeName, new GrpcDotNetNamedPipes.NamedPipeChannelOptions { ConnectionTimeout = _connectionTimeout });
            //var client =  channel.CreateGrpcService(typeof(ITestService));
            return channel.CreateGrpcService<ITestService>();
        }

        public GrpcDotNetNamedPipes.NamedPipeServer CreateServer() => new(_pipeName);

        public override string ToString()
        {
            return "GrpcDotNetNamedPipes";
        }
    }
}