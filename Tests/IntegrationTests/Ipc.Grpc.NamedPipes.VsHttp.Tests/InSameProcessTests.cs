using System;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.TestCaseSource;
using NUnit.Framework;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests
{
    public class InSameProcessTests
    {
        public const int TestTimeout = 3000;

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public void SimpleUnary(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var response = ctx.Client.SimpleUnary(new RequestMessage {Value = 10});
            Assert.That(response.Value, Is.EqualTo(10));
            Assert.True(ctx.Impl.SimplyUnaryCalled);
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task SimpleUnaryAsync(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var response = await ctx.Client.SimpleUnaryAsync(new RequestMessage {Value = 10});
            Assert.That(response.Value, Is.EqualTo(10));
            Assert.True(ctx.Impl.SimplyUnaryCalled);
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task LargePayload(ChannelContextFactory factory)
        {
            var bytes = new byte[1024 * 1024];
            new Random(1234).NextBytes(bytes);
            var byteString = ByteString.CopyFrom(bytes);

            using var ctx = factory.Create();
            var response = await ctx.Client.SimpleUnaryAsync(new RequestMessage {Binary = byteString});
            Assert.That(response.Binary, Is.EqualTo(byteString));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task CancelUnary(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var cts = new CancellationTokenSource();
            var responseTask =
                ctx.Client.DelayedUnaryAsync(new RequestMessage {Value = 10}, cancellationToken: cts.Token);
            cts.Cancel();
            var exception =  Assert.ThrowsAsync<RpcException>(async () => await responseTask);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Cancelled));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task CancelUnaryBeforeCall(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var responseTask =
                ctx.Client.SimpleUnaryAsync(new RequestMessage {Value = 10}, cancellationToken: cts.Token);
            var exception =  Assert.ThrowsAsync<RpcException>(async () => await responseTask);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Cancelled));
            Assert.False(ctx.Impl.SimplyUnaryCalled);
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ThrowingUnary(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var responseTask = ctx.Client.ThrowingUnaryAsync(new RequestMessage {Value = 10});
            var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Unknown));
            Assert.That(exception.Status.Detail, Is.EqualTo("Exception was thrown by handler."));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ThrowCanceledExceptionUnary(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            ctx.Impl.ExceptionToThrow = new OperationCanceledException();
            var responseTask = ctx.Client.ThrowingUnaryAsync(new RequestMessage {Value = 10});
            var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Unknown));
            Assert.That(exception.Status.Detail, Is.EqualTo("Exception was thrown by handler."));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ThrowRpcExceptionUnary(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            ctx.Impl.ExceptionToThrow = new RpcException(new Status(StatusCode.InvalidArgument, "Bad arg"));
            var responseTask = ctx.Client.ThrowingUnaryAsync(new RequestMessage {Value = 10});
            var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.InvalidArgument));
            Assert.That(exception.Status.Detail, Is.EqualTo("Bad arg"));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ThrowAfterCancelUnary(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var cts = new CancellationTokenSource();
            var responseTask =
                ctx.Client.DelayedThrowingUnaryAsync(new RequestMessage {Value = 10}, cancellationToken: cts.Token);
            cts.Cancel();
            var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Cancelled));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ClientStreaming(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var call = ctx.Client.ClientStreaming();
            await call.RequestStream.WriteAsync(new RequestMessage {Value = 3});
            await call.RequestStream.WriteAsync(new RequestMessage {Value = 2});
            await call.RequestStream.WriteAsync(new RequestMessage {Value = 1});
            await call.RequestStream.CompleteAsync();
            var response = await call.ResponseAsync;
            Assert.That(response.Value, Is.EqualTo(6));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task CancelClientStreaming(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var cts = new CancellationTokenSource();
            var call = ctx.Client.ClientStreaming(cancellationToken: cts.Token);
            await call.RequestStream.WriteAsync(new RequestMessage {Value = 1});
            cts.Cancel();
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await call.RequestStream.WriteAsync(new RequestMessage {Value = 1}));
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Cancelled));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public Task CancelClientStreamingBeforeCall(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var call = ctx.Client.ClientStreaming(cancellationToken: cts.Token);
            Assert.ThrowsAsync<TaskCanceledException>(async () => await call.RequestStream.WriteAsync(new RequestMessage {Value = 1}));
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Cancelled));
            return Task.CompletedTask;
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ClientStreamWriteAfterCompletion(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var call = ctx.Client.ClientStreaming();
            await call.RequestStream.WriteAsync(new RequestMessage { Value = 1 });
            await call.RequestStream.CompleteAsync();
            void WriteAction() => call.RequestStream.WriteAsync(new RequestMessage { Value = 1 });
            var exception = Assert.Throws<InvalidOperationException>(WriteAction);
            Assert.That(exception.Message, Is.EqualTo("Request stream has already been completed."));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ServerStreaming(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            AsyncServerStreamingCall<ResponseMessage> call = ctx.Client.ServerStreaming(new RequestMessage {Value = 3});
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(3));
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(2));
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(1));
            Assert.False(await call.ResponseStream.MoveNext());
        }

        [Test,Timeout(3000)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task CancelServerStreaming(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var cts = new CancellationTokenSource();
            var call = ctx.Client.DelayedServerStreaming(new RequestMessage { Value = 3 },
                cancellationToken: cts.Token);
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(3));
            cts.Cancel();
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseStream.MoveNext());
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Cancelled));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task CancelServerStreamingBeforeCall(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var call = ctx.Client.DelayedServerStreaming(new RequestMessage {Value = 3},
                cancellationToken: cts.Token);
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseStream.MoveNext());
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Cancelled));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ThrowingServerStreaming(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var call = ctx.Client.ThrowingServerStreaming(new RequestMessage { Value = 1 });
            Assert.True(await call.ResponseStream.MoveNext());
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseStream.MoveNext());
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Unknown));
            Assert.That(exception.Status.Detail, Is.EqualTo("Exception was thrown by handler."));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ServerStreamWriteAfterCompletion(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var call = ctx.Client.ServerStreaming(new RequestMessage { Value = 1 });
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.False(await call.ResponseStream.MoveNext());
            void WriteAction() => ctx.Impl.ServerStream.WriteAsync(new ResponseMessage { Value = 1 });
            var exception = Assert.Throws<InvalidOperationException>(WriteAction);
            Assert.That(exception.Message, Is.EqualTo("Response stream has already been completed."));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ServerStreamWriteAfterError(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var call = ctx.Client.ThrowingServerStreaming(new RequestMessage { Value = 1 });
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.ThrowsAsync<RpcException>(async () => await call.ResponseStream.MoveNext());
            void WriteAction() => ctx.Impl.ServerStream.WriteAsync(new ResponseMessage { Value = 1 });
            var exception = Assert.Throws<InvalidOperationException>(WriteAction);
            Assert.That(exception.Message, Is.EqualTo("Response stream has already been completed."));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task DuplexStreaming(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var call = ctx.Client.DuplexStreaming();

            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(10));
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(11));

            await call.RequestStream.WriteAsync(new RequestMessage {Value = 3});
            await call.RequestStream.WriteAsync(new RequestMessage {Value = 2});
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(3));
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(2));

            await call.RequestStream.WriteAsync(new RequestMessage {Value = 1});
            await call.RequestStream.CompleteAsync();
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(1));
            Assert.False(await call.ResponseStream.MoveNext());
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task CancelDuplexStreaming(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var cts = new CancellationTokenSource();
            var call = ctx.Client.DelayedDuplexStreaming(cancellationToken: cts.Token);
            await call.RequestStream.WriteAsync(new RequestMessage {Value = 1});
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(1));
            cts.Cancel();
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseStream.MoveNext());
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Cancelled));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task CancelDuplexStreamingBeforeCall(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var call = ctx.Client.DelayedDuplexStreaming(cancellationToken: cts.Token);
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await call.RequestStream.WriteAsync(new RequestMessage {Value = 1}));
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseStream.MoveNext());
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Cancelled));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ThrowingDuplexStreaming(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var call = ctx.Client.ThrowingDuplexStreaming();
            await call.RequestStream.WriteAsync(new RequestMessage {Value = 1});
            await call.RequestStream.CompleteAsync();
            Assert.True(await call.ResponseStream.MoveNext());
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseStream.MoveNext());
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Unknown));
            Assert.That(exception.Status.Detail, Is.EqualTo("Exception was thrown by handler."));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task SetStatus(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var call = ctx.Client.SetStatusAsync(new RequestMessage());
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call);
            Assert.That(exception.Status.StatusCode, Is.EqualTo(StatusCode.InvalidArgument));
            Assert.That(exception.Status.Detail, Is.EqualTo("invalid argument"));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task Deadline(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(1);
            var call = ctx.Client.DelayedUnaryAsync(new RequestMessage(), deadline: deadline);
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.DeadlineExceeded));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task AlreadyExpiredDeadline(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var deadline = DateTime.UtcNow - TimeSpan.FromSeconds(0.1);
            var call = ctx.Client.SimpleUnaryAsync(new RequestMessage(), deadline: deadline);
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.DeadlineExceeded));
            Assert.False(ctx.Impl.SimplyUnaryCalled);
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task HeadersAndTrailers(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var requestHeaders = new Metadata
            {
                {"A1", "1"},
                {"A2-bin", new[] {(byte) 2}},
            };
            var responseHeaders = new Metadata
            {
                {"B1", "1"},
                {"B2-bin", new[] {(byte) 2}},
            };
            var responseTrailers = new Metadata
            {
                {"C1", "1"},
                {"C2-bin", new[] {(byte) 2}},
            };

            ctx.Impl.ResponseHeaders = responseHeaders;
            ctx.Impl.ResponseTrailers = responseTrailers;
            AsyncUnaryCall<ResponseMessage> call = ctx.Client.HeadersTrailersAsync(new RequestMessage {Value = 1}, requestHeaders);

            var actualResponseHeaders = await call.ResponseHeadersAsync;
            await call.ResponseAsync;
            var actualResponseTrailers = call.GetTrailers();
            var actualStatus = call.GetStatus();
            var actualRequestHeaders = ctx.Impl.RequestHeaders;

            AssertHasMetadata(requestHeaders, actualRequestHeaders);
            AssertHasMetadata(responseHeaders, actualResponseHeaders);
            AssertHasMetadata(responseTrailers, actualResponseTrailers);
            Assert.That(actualStatus.StatusCode, Is.EqualTo(StatusCode.OK));
        }

        private void AssertHasMetadata(Metadata expected, Metadata actual)
        {
            var actualDict = actual.ToDictionary(x => x.Key);
            foreach (var expectedEntry in expected)
            {
                Assert.True(actualDict.ContainsKey(expectedEntry.Key));
                var actualEntry = actualDict[expectedEntry.Key];
                Assert.That(actualEntry.IsBinary, Is.EqualTo(expectedEntry.IsBinary));
                if (expectedEntry.IsBinary)
                {
                    Assert.That(actualEntry.ValueBytes.AsEnumerable(), Is.EqualTo(expectedEntry.ValueBytes.AsEnumerable()));
                }
                else
                {
                    Assert.That(actualEntry.Value, Is.EqualTo(expectedEntry.Value));
                }
            }
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public void ConnectionTimeout(ChannelContextFactory factory)
        {
            var client = factory.CreateClient();
            var exception = Assert.Throws<RpcException>(() => client.SimpleUnary(new RequestMessage { Value = 10 }));
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Unavailable));
            Assert.That(exception.Status.Detail, Is.EqualTo("failed to connect to all addresses"));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(GrpcDotNetNamedPipesChannelSource))]
        public async Task CancellationRace(GrpcDotNetNamedPipesChannelFactory factory)
        {
            using var ctx = factory.Create();
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

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(GrpcDotNetNamedPipesChannelSource))]
        public void CallImmediatelyAfterKillingServer(GrpcDotNetNamedPipesChannelFactory factory)
        {
            using var ctx = factory.Create();
            ctx.Dispose();
            var exception = Assert.Throws<RpcException>(() => ctx.Client.SimpleUnary(new RequestMessage { Value = 10 }));
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Unavailable));
            Assert.That(exception.Status.Detail, Is.EqualTo("failed to connect to all addresses"));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task RestartServerAfterCall(ChannelContextFactory factory)
        {
            using var ctx1 = factory.Create();
            var response1 = await ctx1.Client.SimpleUnaryAsync(new RequestMessage { Value = 10 });
            Assert.That(response1.Value, Is.EqualTo(10));

            await Task.Delay(500);
            ctx1.Dispose();
            await Task.Delay(500);

            using var ctx2 = factory.Create();
            var response2 = await ctx2.Client.SimpleUnaryAsync(new RequestMessage { Value = 10 });
            Assert.That(response2.Value, Is.EqualTo(10));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task RestartServerAfterNoCalls(ChannelContextFactory factory)
        {
            using var ctx1 = factory.Create();

            await Task.Delay(500);
            ctx1.Dispose();
            await Task.Delay(500);

            using var ctx2 = factory.Create();
            var response2 = await ctx2.Client.SimpleUnaryAsync(new RequestMessage { Value = 10 });
            Assert.That(response2.Value, Is.EqualTo(10));
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(NamedPipeChannelSource))]
        public void StartServerAfterStop(NamedPipeChannelContextFactory factory)
        {
            var server = factory.CreateServer();
            server.Start();
            server.Kill();
            Assert.Throws<ObjectDisposedException>(() => server.Start());
        }

#if NET_5_0 || NETFRAMEWORK
        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(GrpcDotNetNamedPipesChannelSource))]
        public void SimpleUnaryWithACLs(GrpcDotNetNamedPipesChannelFactory factory)
        {
            var security = new PipeSecurity();
            var sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.ReadWrite, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));

            var options = new GrpcDotNetNamedPipes.NamedPipeServerOptions { PipeSecurity = security };

            using var ctx = factory.Create(options);
            var response = ctx.Client.SimpleUnary(new RequestMessage { Value = 10 });
            Assert.That(response.Value, Is.EqualTo(10));
            Assert.True(ctx.Impl.SimplyUnaryCalled);
        }

        [Test,Timeout(TestTimeout)]
        [TestCaseSource(typeof(GrpcDotNetNamedPipesChannelSource))]
        public void SimpleUnaryWithACLsDenied(GrpcDotNetNamedPipesChannelFactory factory)
        {
            var security = new PipeSecurity();
            var sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.ReadWrite, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.ReadWrite, AccessControlType.Deny));

            var options = new GrpcDotNetNamedPipes.NamedPipeServerOptions { PipeSecurity = security };

            using var ctx = factory.Create(options);
            var exception = Assert.Throws<UnauthorizedAccessException>(() => ctx.Client.SimpleUnary(new RequestMessage { Value = 10 }));
        }
#endif
    }
}