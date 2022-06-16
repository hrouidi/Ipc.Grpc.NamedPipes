using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
using NUnit.Framework;
using MultiChannelSource = Ipc.Grpc.NamedPipes.VsUnixDomainSocket.Tests.CaseSources.MultiChannelSource;

namespace Ipc.Grpc.NamedPipes.VsUnixDomainSocket.Tests
{
    public class UnaryTests
    {
        public const int TestTimeout = 3000;

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public void SimpleUnary(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            ResponseMessage response = ctx.Client.SimpleUnary(new RequestMessage { Value = 10 });
            Assert.That(response.Value, Is.EqualTo(10));
            Assert.True(ctx.Impl.SimplyUnaryCalled);
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task SimpleUnaryAsync(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            ResponseMessage response = await ctx.Client.SimpleUnaryAsync(new RequestMessage { Value = 10 });
            Assert.That(response.Value, Is.EqualTo(10));
            Assert.True(ctx.Impl.SimplyUnaryCalled);
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task LargePayload(ChannelContextFactory factory)
        {
            var bytes = new byte[1024 * 1024];
            new Random(1234).NextBytes(bytes);
            ByteString byteString = ByteString.CopyFrom(bytes);

            using ChannelContext ctx = factory.Create();
            ResponseMessage response = await ctx.Client.SimpleUnaryAsync(new RequestMessage { Binary = byteString });
            Assert.That(response.Binary, Is.EqualTo(byteString));
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public Task CancelUnary(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            var cts = new CancellationTokenSource();
            var responseTask = ctx.Client.DelayedUnaryAsync(new RequestMessage { Value = 10 }, cancellationToken: cts.Token);
            cts.Cancel();
            var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Cancelled));
            return Task.CompletedTask;
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public Task CancelUnaryBeforeCall(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var responseTask = ctx.Client.SimpleUnaryAsync(new RequestMessage { Value = 10 }, cancellationToken: cts.Token);
            var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Cancelled));
            Assert.False(ctx.Impl.SimplyUnaryCalled);
            return Task.CompletedTask;
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public Task ThrowingUnary(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            var responseTask = ctx.Client.ThrowingUnaryAsync(new RequestMessage { Value = 10 });
            var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Unknown));
            Assert.That(exception.Status.Detail, Is.EqualTo("Exception was thrown by handler."));
            return Task.CompletedTask;
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public Task ThrowCanceledExceptionUnary(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            ctx.Impl.ExceptionToThrow = new OperationCanceledException();
            var responseTask = ctx.Client.ThrowingUnaryAsync(new RequestMessage { Value = 10 });
            var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Unknown));
            Assert.That(exception.Status.Detail, Is.EqualTo("Exception was thrown by handler."));
            return Task.CompletedTask;
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public Task ThrowRpcExceptionUnary(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            ctx.Impl.ExceptionToThrow = new RpcException(new Status(StatusCode.InvalidArgument, "Bad arg"));
            var responseTask = ctx.Client.ThrowingUnaryAsync(new RequestMessage { Value = 10 });
            var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.InvalidArgument));
            Assert.That(exception.Status.Detail, Is.EqualTo("Bad arg"));
            return Task.CompletedTask;
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public Task ThrowAfterCancelUnary(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            var cts = new CancellationTokenSource();
            var responseTask =
                ctx.Client.DelayedThrowingUnaryAsync(new RequestMessage { Value = 10 }, cancellationToken: cts.Token);
            cts.Cancel();
            var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Cancelled));
            return Task.CompletedTask;
        }


        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task SetStatus(ChannelContextFactory factory)
        {

        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public Task Deadline(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            DateTime deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(1);
            var call = ctx.Client.DelayedUnaryAsync(new RequestMessage(), deadline: deadline);
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.DeadlineExceeded));
            return Task.CompletedTask;
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public Task AlreadyExpiredDeadline(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            DateTime deadline = DateTime.UtcNow - TimeSpan.FromSeconds(0.1);
            var call = ctx.Client.SimpleUnaryAsync(new RequestMessage(), deadline: deadline);
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.DeadlineExceeded));
            Assert.False(ctx.Impl.SimplyUnaryCalled);
            return Task.CompletedTask;
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task HeadersAndTrailers(ChannelContextFactory factory)
        {

        }


        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public void ConnectionTimeout(ChannelContextFactory factory)
        {
            TestService.TestServiceClient client = factory.CreateClient();
            var exception = Assert.Throws<RpcException>(() => client.SimpleUnary(new RequestMessage { Value = 10 }));
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Unavailable));
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task CancellationRace(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            var random = new Random();
            for (int i = 0; i < 200; i++)
            {
                var cts = new CancellationTokenSource(random.Next(10));
                var response = ctx.Client.SimpleUnaryAsync(new RequestMessage { Value = 10 }, cancellationToken: cts.Token);
                try
                {
                    Assert.That((await response).Value, Is.EqualTo(10));
                }
                catch (RpcException ex)
                {
                    Assert.That(ex.StatusCode, Is.EqualTo(StatusCode.Cancelled));
                }
            }
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public void CallImmediatelyAfterKillingServer(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            ctx.Dispose();
            var exception = Assert.Throws<RpcException>(() => ctx.Client.SimpleUnary(new RequestMessage { Value = 10 }));
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Unavailable));
            Assert.That(exception.Status.Detail, Is.EqualTo("failed to connect to all addresses"));
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task RestartServerAfterCall(ChannelContextFactory factory)
        {
            using ChannelContext ctx1 = factory.Create();
            ResponseMessage response1 = await ctx1.Client.SimpleUnaryAsync(new RequestMessage { Value = 10 });
            Assert.That(response1.Value, Is.EqualTo(10));

            await Task.Delay(500);
            ctx1.Dispose();
            await Task.Delay(500);

            using ChannelContext ctx2 = factory.Create();
            ResponseMessage response2 = await ctx2.Client.SimpleUnaryAsync(new RequestMessage { Value = 10 });
            Assert.That(response2.Value, Is.EqualTo(10));
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task RestartServerAfterNoCalls(ChannelContextFactory factory)
        {
            using ChannelContext ctx1 = factory.Create();

            await Task.Delay(500);
            ctx1.Dispose();
            await Task.Delay(500);

            using ChannelContext ctx2 = factory.Create();
            ResponseMessage response2 = await ctx2.Client.SimpleUnaryAsync(new RequestMessage { Value = 10 });
            Assert.That(response2.Value, Is.EqualTo(10));
        }

#if NET_5_0 || NETFRAMEWORK
        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(GrpcDotNetNamedPipesChannelSource))]
        public void SimpleUnaryWithACLs(GrpcDotNetNamedPipesChannelFactory factory)
        {
            var security = new PipeSecurity();
            var sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.ReadWrite, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));

            var options = new GrpcDotNetNamedPipes.NamedPipeServerOptions { PipeSecurity = security };

            using ChannelContext ctx = factory.Create(options);
            ResponseMessage response = ctx.Client.SimpleUnary(new RequestMessage { Value = 10 });
            Assert.That(response.Value, Is.EqualTo(10));
            Assert.True(ctx.Impl.SimplyUnaryCalled);
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(GrpcDotNetNamedPipesChannelSource))]
        public void SimpleUnaryWithACLsDenied(GrpcDotNetNamedPipesChannelFactory factory)
        {
            var security = new PipeSecurity();
            var sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.ReadWrite, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.ReadWrite, AccessControlType.Deny));

            var options = new GrpcDotNetNamedPipes.NamedPipeServerOptions { PipeSecurity = security };

            using ChannelContext ctx = factory.Create(options);
            var exception = Assert.Throws<UnauthorizedAccessException>(() => ctx.Client.SimpleUnary(new RequestMessage { Value = 10 }));
        }
#endif
    }
}