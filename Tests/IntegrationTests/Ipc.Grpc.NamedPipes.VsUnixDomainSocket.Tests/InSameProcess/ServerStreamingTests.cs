using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
using NUnit.Framework;
using MultiChannelSource = Ipc.Grpc.NamedPipes.VsUnixDomainSocket.Tests.CaseSources.MultiChannelSource;

namespace Ipc.Grpc.NamedPipes.VsUnixDomainSocket.Tests.InSameProcess
{
    public class ServerStreamingTests
    {
        public const int TestTimeout = 3000;


        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ServerStreaming(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            AsyncServerStreamingCall<ResponseMessage> call = ctx.Client.ServerStreaming(new RequestMessage { Value = 3 });
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(3));
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(2));
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(1));
            Assert.False(await call.ResponseStream.MoveNext());
        }

        [Test, Timeout(3000)]
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

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public Task CancelServerStreamingBeforeCall(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var call = ctx.Client.DelayedServerStreaming(new RequestMessage { Value = 3 },
                cancellationToken: cts.Token);
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseStream.MoveNext());
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Cancelled));
            return Task.CompletedTask;
        }

        [Test, Timeout(TestTimeout)]
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

        [Test, Timeout(TestTimeout)]
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

        [Test, Timeout(TestTimeout)]
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


        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public Task SetStatus(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var call = ctx.Client.SetStatusAsync(new RequestMessage());
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call);
            Assert.That(exception.Status.StatusCode, Is.EqualTo(StatusCode.InvalidArgument));
            Assert.That(exception.Status.Detail, Is.EqualTo("invalid argument"));
            return Task.CompletedTask;
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public Task Deadline(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(1);
            var call = ctx.Client.DelayedUnaryAsync(new RequestMessage(), deadline: deadline);
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.DeadlineExceeded));
            return Task.CompletedTask;
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public Task AlreadyExpiredDeadline(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var deadline = DateTime.UtcNow - TimeSpan.FromSeconds(0.1);
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
            AsyncUnaryCall<ResponseMessage> call = ctx.Client.HeadersTrailersAsync(new RequestMessage { Value = 1 }, requestHeaders);

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

    }
}