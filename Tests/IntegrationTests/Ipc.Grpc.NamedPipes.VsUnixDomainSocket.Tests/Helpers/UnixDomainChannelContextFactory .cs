using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.Tests.ProtoContract;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Ipc.Grpc.NamedPipes.VsUnixDomainSocket.Tests.Helpers
{
    public class UnixDomainChannelContextFactory : ChannelContextFactory
    {
        private static readonly string _socketPath = Path.Combine(Path.GetTempPath(), "socket.tmp");

        public override ChannelContext Create()
        {
            var impl = new TestServiceImplementation();
            WebApplication server = CreateServer();
            server.RunAsync();

            return new ChannelContext
            {
                Impl = impl,
                Client = CreateClient(),
                OnDispose = () => server.StopAsync().Wait()
            };
        }

        public override TestService.TestServiceClient CreateClient()
        {
            var udsEndPoint = new UnixDomainSocketEndPoint(_socketPath);
            var connectionFactory = new UnixDomainSocketConnectionFactory(udsEndPoint);
            var socketsHttpHandler = new SocketsHttpHandler
            {
                ConnectCallback = connectionFactory.ConnectAsync
            };

            var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
            {
                HttpHandler = socketsHttpHandler
            });
            return new TestService.TestServiceClient(channel);
        }

        private static WebApplication CreateServer()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();

            // Add services to the container.
            builder.Services.AddGrpc();
            builder.WebHost.ConfigureKestrel(options =>
            {
                if (File.Exists(_socketPath))
                    File.Delete(_socketPath);
                options.ListenUnixSocket(_socketPath, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http2;
                });
            });



            WebApplication app = builder.Build();

            // Configure the HTTP request pipeline.
            app.MapGrpcService<TestServiceImplementation>();
            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
            return app;
        }

        public override string ToString()
        {
            return "UnixDomain";
        }

        public class UnixDomainSocketConnectionFactory
        {
            private readonly EndPoint _endPoint;

            public UnixDomainSocketConnectionFactory(EndPoint endPoint)
            {
                _endPoint = endPoint;
            }

            public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext _, CancellationToken cancellationToken = default)
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

                try
                {
                    await socket.ConnectAsync(_endPoint, cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        }
    }
}