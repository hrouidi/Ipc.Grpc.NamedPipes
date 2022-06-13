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

        

    }
}