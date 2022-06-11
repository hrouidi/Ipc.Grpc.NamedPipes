using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.CaseSources;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
using NUnit.Framework;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests.InSameProcess
{
    public class ClientStreamingTests
    {
        public const int TestTimeout = 3000;

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ClientStreaming(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            var call = ctx.Client.ClientStreaming();
            await call.RequestStream.WriteAsync(new RequestMessage { Value = 3 });
            await call.RequestStream.WriteAsync(new RequestMessage { Value = 2 });
            await call.RequestStream.WriteAsync(new RequestMessage { Value = 1 });
            await call.RequestStream.CompleteAsync();
            ResponseMessage response = await call.ResponseAsync;
            Assert.That(response.Value, Is.EqualTo(6));
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task CancelClientStreaming(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            var cts = new CancellationTokenSource();
            var call = ctx.Client.ClientStreaming(cancellationToken: cts.Token);
            await call.RequestStream.WriteAsync(new RequestMessage { Value = 1 });
            cts.Cancel();
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await call.RequestStream.WriteAsync(new RequestMessage { Value = 1 }));
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Cancelled));
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public Task CancelClientStreamingBeforeCall(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var call = ctx.Client.ClientStreaming(cancellationToken: cts.Token);
            Assert.ThrowsAsync<TaskCanceledException>(async () => await call.RequestStream.WriteAsync(new RequestMessage { Value = 1 }));
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Cancelled));
            return Task.CompletedTask;
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ClientStreamWriteAfterCompletion(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            var call = ctx.Client.ClientStreaming();
            await call.RequestStream.WriteAsync(new RequestMessage { Value = 1 });
            await call.RequestStream.CompleteAsync();
            void WriteAction() => call.RequestStream.WriteAsync(new RequestMessage { Value = 1 });
            var exception = Assert.Throws<InvalidOperationException>(WriteAction);
            Assert.That(exception.Message, Is.EqualTo("Request stream has already been completed."));
        }

        


        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public Task SetStatus(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
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
            using ChannelContext ctx = factory.Create();
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

            Metadata actualResponseHeaders = await call.ResponseHeadersAsync;
            await call.ResponseAsync;
            Metadata actualResponseTrailers = call.GetTrailers();
            Status actualStatus = call.GetStatus();
            Metadata actualRequestHeaders = ctx.Impl.RequestHeaders;

            AssertHasMetadata(requestHeaders, actualRequestHeaders);
            AssertHasMetadata(responseHeaders, actualResponseHeaders);
            AssertHasMetadata(responseTrailers, actualResponseTrailers);
            Assert.That(actualStatus.StatusCode, Is.EqualTo(StatusCode.OK));
        }

        private void AssertHasMetadata(Metadata expected, Metadata actual)
        {
            var actualDict = actual.ToDictionary(x => x.Key);
            foreach (Metadata.Entry expectedEntry in expected)
            {
                Assert.True(actualDict.ContainsKey(expectedEntry.Key));
                Metadata.Entry actualEntry = actualDict[expectedEntry.Key];
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