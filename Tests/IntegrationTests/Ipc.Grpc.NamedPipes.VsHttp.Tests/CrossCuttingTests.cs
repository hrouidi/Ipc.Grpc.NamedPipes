using System;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.Tests.ProtoContract;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.CaseSources;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
using NUnit.Framework;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests
{
    public class CrossCuttingTests
    {
        public const int TestTimeout = 3000;

        [Test]
        public async Task ServerInterceptors_Tests()
        {
            var impl = new TestServiceImplementation();
            var server = new NamedPipeServer("def");
            IdleTimeInterceptor idleTimeInterceptor = new(TimeSpan.FromSeconds(5), () =>
            {
                server.Shutdown();
            });

            server.AddInterceptor(idleTimeInterceptor);
            TestService.BindService(server.ServiceBinder, impl);

            idleTimeInterceptor.Start();
            await server.RunAsync();
        }


        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public void ConnectionTimeout(ChannelContextFactory factory)
        {
            TestService.TestServiceClient client = factory.CreateClient();
            var exception = Assert.Throws<RpcException>(() => client.SimpleUnary(new RequestMessage { Value = 10 }));
            Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.Unavailable));
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(GrpcDotNetNamedPipesChannelSource))]
        public void CallImmediatelyAfterKillingServer(GrpcDotNetNamedPipesChannelFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            ctx.Dispose();
            var exception = Assert.Throws<RpcException>(() => ctx.Client.SimpleUnary(new RequestMessage { Value = 10 }));
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Unavailable));
            Assert.That(exception.Status.Detail, Is.EqualTo("failed to connect to all addresses"));
        }

        [Test, Timeout(TestTimeout),Explicit]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task RestartServerAfterCall(ChannelContextFactory factory)
        {
            ChannelContext ctx1 = factory.Create();
            ResponseMessage response1 = await ctx1.Client.SimpleUnaryAsync(new RequestMessage { Value = 10 });
            Assert.That(response1.Value, Is.EqualTo(10));

            await Task.Delay(1);
            ctx1.Dispose();
            await Task.Delay(1);

            using ChannelContext ctx2 = factory.Create();
            ResponseMessage response2 = await ctx2.Client.SimpleUnaryAsync(new RequestMessage { Value = 10 });
            Assert.That(response2.Value, Is.EqualTo(10));
        }

        [Test, Timeout(TestTimeout), Explicit]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task RestartServerAfterNoCalls(ChannelContextFactory factory)
        {
            ChannelContext ctx1 = factory.Create();

            await Task.Delay(1);
            ctx1.Dispose();
            await Task.Delay(1);

            using ChannelContext ctx2 = factory.Create();
            ResponseMessage response2 = await ctx2.Client.SimpleUnaryAsync(new RequestMessage { Value = 10 });
            Console.WriteLine("call succeed");
            Assert.That(response2.Value, Is.EqualTo(10));
        }
    }
}