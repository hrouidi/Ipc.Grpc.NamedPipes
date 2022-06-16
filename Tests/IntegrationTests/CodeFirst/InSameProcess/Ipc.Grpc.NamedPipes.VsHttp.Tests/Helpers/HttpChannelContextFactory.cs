using System.Linq;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Tests.ProtoContract;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Server;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers
{
    public class HttpChannelContextFactory : ChannelContextFactory
    {
        private static readonly ChannelOption[] Options =
        {
            new(ChannelOptions.MaxReceiveMessageLength, 1024 * 1024 * 1024),
            new(ChannelOptions.MaxSendMessageLength, 1024 * 1024 * 1024)
        };
        private int _port;

        public override ChannelContext Create()
        {
            var impl = new TestServiceImplementation();
            var server = new Server(Options)
            {
                Ports = { new ServerPort("localhost", 0, ServerCredentials.Insecure) }
            };
            server.Services.AddCodeFirst(impl);
            server.Start();
            _port = server.Ports.First().BoundPort;
            return new ChannelContext
            {
                Impl = impl,
                Client = CreateClient(),
                OnDispose = () => server.KillAsync()
            };
        }

        public override ITestService CreateClient()
        {
            var channel = new Channel(
                "localhost",
                _port,
                ChannelCredentials.Insecure,
                Options);
            return channel.CreateGrpcService<ITestService>();
        }

        public override string ToString()
        {
            return "http";
        }
    }
}