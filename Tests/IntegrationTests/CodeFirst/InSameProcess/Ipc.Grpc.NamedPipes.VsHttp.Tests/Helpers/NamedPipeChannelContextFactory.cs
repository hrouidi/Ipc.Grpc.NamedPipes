using System;
using Ipc.Grpc.NamedPipes.Tests.ProtoContract;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers.Extensions;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers
{
    public class NamedPipeChannelContextFactory : ChannelContextFactory
    {
        private readonly string _pipeName = $"Ipc.Grpc.NamedPipes/{Guid.NewGuid()}";

        //private const int _connectionTimeout = -1;
        private const int _connectionTimeout = 100;

        public ChannelContext Create(NamedPipeServerOptions options)
        {

            var impl = new TestServiceImplementation();
            var server = new NamedPipeServer(_pipeName, options);
            server.Bind<ITestService>(impl);
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

        public override ITestService CreateClient()
        {
            var channel = new NamedPipeChannel(_pipeName, new NamedPipeChannelOptions { ConnectionTimeout = _connectionTimeout });
            return channel.CreateGrpcService<ITestService>();
        }

        public NamedPipeServer CreateServer() => new(_pipeName);

        public override string ToString()
        {
            return "Ipc.Grpc.NamedPipes";
        }
    }
}