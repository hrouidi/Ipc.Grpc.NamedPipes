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
    public class DuplexStreamingTests
    {
        public const int TestTimeout = 3000;

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task DuplexStreaming(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var call = ctx.Client.DuplexStreaming();

            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(10));
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(11));

            await call.RequestStream.WriteAsync(new RequestMessage { Value = 3 });
            await call.RequestStream.WriteAsync(new RequestMessage { Value = 2 });
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(3));
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(2));

            await call.RequestStream.WriteAsync(new RequestMessage { Value = 1 });
            await call.RequestStream.CompleteAsync();
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(1));
            Assert.False(await call.ResponseStream.MoveNext());
        }


        //[Test, Timeout(TestTimeout)]
        //[TestCaseSource(typeof(MultiChannelSource))]
        public async Task SetStatus(ChannelContextFactory factory)
        {

        }

        //[Test, Timeout(TestTimeout)]
        //[TestCaseSource(typeof(MultiChannelSource))]
        public async Task SetHeadersAndTrailers(ChannelContextFactory factory)
        {

        }

        #region Cancel tests

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task CancelDuplexStreaming(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var cts = new CancellationTokenSource();
            var call = ctx.Client.DelayedDuplexStreaming(cancellationToken: cts.Token);
            await call.RequestStream.WriteAsync(new RequestMessage { Value = 1 });
            Assert.True(await call.ResponseStream.MoveNext());
            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(1));
            cts.Cancel();
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseStream.MoveNext());
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Cancelled));
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public Task CancelDuplexStreamingBeforeCall(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var call = ctx.Client.DelayedDuplexStreaming(cancellationToken: cts.Token);
            Assert.ThrowsAsync<TaskCanceledException>(async () => await call.RequestStream.WriteAsync(new RequestMessage { Value = 1 }));
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseStream.MoveNext());
            Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.Cancelled));
            return Task.CompletedTask;
        }

        #endregion

        #region Dispose tests

        #endregion

        #region Deadline tests

        #endregion

        #region Exceptions forwarding tests

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task ThrowingDuplexStreaming(ChannelContextFactory factory)
        {
            using var ctx = factory.Create();
            var call = ctx.Client.ThrowingDuplexStreaming();
            await call.RequestStream.WriteAsync(new RequestMessage { Value = 1 });
            await call.RequestStream.CompleteAsync();
            Assert.True(await call.ResponseStream.MoveNext());
            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseStream.MoveNext());
            Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.Unknown));
            StringAssert.StartsWith("Exception was thrown by handler",exception.Status.Detail);
        }

        #endregion
    }
}