using System;
using Ipc.Grpc.NamedPipes.Tests.ProtoContract;
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
            NativeMarshaller.Setup();
            server.ServiceBinder.AddCodeFirst(impl);
            server.StartAsync();
            return new ChannelContext
            {
                Impl = impl,
                Client = CreateClient(),
                OnDispose = () => server.Dispose()
            };
        }

        public override ChannelContext Create()
        {
            return Create(new NamedPipeServerOptions());
        }

        public override ITestService CreateClient()
        {
            var channel = new NamedPipeChannel(_pipeName, new NamedPipeChannelOptions { ConnectionTimeout = _connectionTimeout });
            NativeMarshaller.Setup();
            return channel.CreateGrpcService<ITestService>();
        }

        public NamedPipeServer CreateServer() => new(_pipeName);

        public override string ToString()
        {
            return "Ipc.Grpc.NamedPipes";
        }
    }
}