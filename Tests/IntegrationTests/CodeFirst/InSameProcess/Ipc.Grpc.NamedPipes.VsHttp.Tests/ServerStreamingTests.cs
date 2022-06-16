//using System;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using Grpc.Core;
//using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
//using Ipc.Grpc.NamedPipes.VsHttp.Tests.CaseSources;
//using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
//using NUnit.Framework;

//namespace Ipc.Grpc.NamedPipes.VsHttp.Tests
//{
//    public class ServerStreamingTests
//    {
//        public const int TestTimeout = 3000;

//        [Test, Timeout(TestTimeout)]
//        [TestCaseSource(typeof(MultiChannelSource))]
//        public async Task ServerStreaming(ChannelContextFactory factory)
//        {
//            using var ctx = factory.Create();
//            AsyncServerStreamingCall<ResponseMessage> call = ctx.Client.ServerStreaming(new RequestMessage { Value = 3 });
//            Assert.True(await call.ResponseStream.MoveNext());
//            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(3));
//            Assert.True(await call.ResponseStream.MoveNext());
//            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(2));
//            Assert.True(await call.ResponseStream.MoveNext());
//            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(1));
//            Assert.False(await call.ResponseStream.MoveNext());
//        }

//        [Test, Timeout(TestTimeout)]
//        [TestCaseSource(typeof(MultiChannelSource))]
//        public async Task ServerStreamWriteAfterCompletion(ChannelContextFactory factory)
//        {
//            using var ctx = factory.Create();
//            var call = ctx.Client.ServerStreaming(new RequestMessage { Value = 1 });
//            Assert.True(await call.ResponseStream.MoveNext());
//            Assert.False(await call.ResponseStream.MoveNext());
//            //void WriteAction() => ;
//            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await ctx.Impl.ServerStream.WriteAsync(new ResponseMessage { Value = 1 }));
//            Assert.That(exception!.Message, Is.EqualTo("Response stream has already been completed."));
//        }

//        [Test, Timeout(TestTimeout)]
//        [TestCaseSource(typeof(MultiChannelSource))]
//        public async Task ServerStreamWriteAfterError(ChannelContextFactory factory)
//        {
//            using var ctx = factory.Create();
//            var call = ctx.Client.ThrowingServerStreaming(new RequestMessage { Value = 1 });
//            Assert.True(await call.ResponseStream.MoveNext());
//            Assert.ThrowsAsync<RpcException>(async () => await call.ResponseStream.MoveNext());
//            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await ctx.Impl.ServerStream.WriteAsync(new ResponseMessage { Value = 1 }));
//            Assert.That(exception!.Message, Is.EqualTo("Response stream has already been completed."));
//        }

//        //[Test, Timeout(TestTimeout)]
//        //[TestCaseSource(typeof(MultiChannelSource))]
//        public void SetStatus(ChannelContextFactory factory)
//        {

//        }

//        //[Test, Timeout(TestTimeout)]
//        //[TestCaseSource(typeof(MultiChannelSource))]
//        public async Task SetHeadersAndTrailers(ChannelContextFactory factory)
//        {

//        }

//        #region Cancel tests

//        [Test, Timeout(3000)]
//        [TestCaseSource(typeof(MultiChannelSource))]
//        public async Task CancelServerStreaming(ChannelContextFactory factory)
//        {
//            using var ctx = factory.Create();
//            var cts = new CancellationTokenSource();
//            var call = ctx.Client.DelayedServerStreaming(new RequestMessage { Value = 3 }, cancellationToken: cts.Token);
//            Assert.True(await call.ResponseStream.MoveNext());
//            Assert.That(call.ResponseStream.Current.Value, Is.EqualTo(3));
//            cts.Cancel();
//            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseStream.MoveNext());
//            Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.Cancelled));
//        }

//        [Test, Timeout(TestTimeout)]
//        [TestCaseSource(typeof(MultiChannelSource))]
//        public Task CancelServerStreamingBeforeCall(ChannelContextFactory factory)
//        {
//            using var ctx = factory.Create();
//            var cts = new CancellationTokenSource();
//            cts.Cancel();
//            var call = ctx.Client.DelayedServerStreaming(new RequestMessage { Value = 3 }, cancellationToken: cts.Token);
//            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseStream.MoveNext());
//            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Cancelled));
//            return Task.CompletedTask;
//        }

//        #endregion

//        #region Dispose tests

//        [Test, Timeout(TestTimeout)]
//        [TestCaseSource(typeof(MultiChannelSource))]
//        public async Task DisposeWhileAwaiting_ShouldSendCancelRemoteAndThrowCanceledRpcException_Test(ChannelContextFactory factory)
//        {

//            using ChannelContext ctx = factory.Create();
//            AsyncServerStreamingCall<ResponseMessage> call = ctx.Client.DelayedServerStreaming(new RequestMessage { Value = 3 });
//            Assert.True(await call.ResponseStream.MoveNext());
//            call.Dispose();

//            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseStream.MoveNext());
//            Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.Cancelled));
//        }

//        #endregion

//        #region Deadline tests

//        #endregion

//        #region Exceptions forwarding tests

//        [Test, Timeout(TestTimeout)]
//        [TestCaseSource(typeof(MultiChannelSource))]
//        public async Task ThrowingServerStreaming(ChannelContextFactory factory)
//        {
//            using var ctx = factory.Create();
//            var call = ctx.Client.ThrowingServerStreaming(new RequestMessage { Value = 1 });
//            Assert.True(await call.ResponseStream.MoveNext());
//            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseStream.MoveNext());
//            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Unknown));
//            StringAssert.StartsWith("Exception was thrown by handler", exception.Status.Detail);
//        }

//        #endregion
//    }
//}