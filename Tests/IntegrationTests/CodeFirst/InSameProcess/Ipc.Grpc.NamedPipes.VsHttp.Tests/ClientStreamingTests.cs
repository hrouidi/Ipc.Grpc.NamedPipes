//using System;
//using System.Threading;
//using System.Threading.Tasks;
//using Grpc.Core;
//using Ipc.Grpc.NamedPipes.ContractFirstTests.ProtoGenerated;
//using Ipc.Grpc.NamedPipes.VsHttp.Tests.CaseSources;
//using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
//using NUnit.Framework;

//namespace Ipc.Grpc.NamedPipes.VsHttp.Tests
//{
//    public class ClientStreamingTests
//    {
//        public const int TestTimeout = 3000;

//        [Test, Timeout(TestTimeout)]
//        [TestCaseSource(typeof(MultiChannelSource))]
//        public async Task ClientStreaming(ChannelContextFactory factory)
//        {
//            using ChannelContext ctx = factory.Create();
//            var call = ctx.Client.ClientStreaming();
//            await call.RequestStream.WriteAsync(new RequestMessage { Value = 3 });
//            await call.RequestStream.WriteAsync(new RequestMessage { Value = 2 });
//            await call.RequestStream.WriteAsync(new RequestMessage { Value = 1 });
//            await call.RequestStream.CompleteAsync();
//            ResponseMessage response = await call.ResponseAsync;
//            Assert.That(response.Value, Is.EqualTo(6));
//        }

//        [Test, Timeout(TestTimeout)]
//        [TestCaseSource(typeof(MultiChannelSource))]
//        public async Task WriteAfterCompletionShouldThrowException(ChannelContextFactory factory)
//        {
//            using ChannelContext ctx = factory.Create();
//            var call = ctx.Client.ClientStreaming();
//            await call.RequestStream.WriteAsync(new RequestMessage { Value = 1 });
//            await call.RequestStream.CompleteAsync();
//            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await call.RequestStream.WriteAsync(new RequestMessage { Value = 1 }));
//            Assert.That(exception!.Message, Is.EqualTo("Request stream has already been completed."));
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

//        #region Cancellation tests

//        [Test, Timeout(TestTimeout)]
//        [TestCaseSource(typeof(MultiChannelSource))]
//        public Task CancelBeforeBeginStreaming(ChannelContextFactory factory)
//        {
//            using ChannelContext ctx = factory.Create();
//            var cts = new CancellationTokenSource();
//            cts.Cancel();
//            var call = ctx.Client.ClientStreaming(cancellationToken: cts.Token);
//            Assert.CatchAsync<Exception>(async () => await call.RequestStream.WriteAsync(new RequestMessage { Value = 1 }));
//            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);
//            Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.Cancelled));
//            return Task.CompletedTask;
//        }

//        [Test, Timeout(TestTimeout)]
//        [TestCaseSource(typeof(MultiChannelSource))]
//        public async Task CancelWhileStreaming(ChannelContextFactory factory)
//        {
//            using ChannelContext ctx = factory.Create();
//            var cts = new CancellationTokenSource();
//            var call = ctx.Client.ClientStreaming(cancellationToken: cts.Token);
//            await call.RequestStream.WriteAsync(new RequestMessage { Value = 1 });
//            cts.Cancel();
//            Assert.ThrowsAsync<TaskCanceledException>(async () => await call.RequestStream.WriteAsync(new RequestMessage { Value = 1 }));
//            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);
//            Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.Cancelled));
//        }

//        #endregion

//        #region Deadline test

//        [Test, Timeout(TestTimeout)]
//        [TestCaseSource(typeof(MultiChannelSource))]
//        public async Task DeadlineExpiredWhileStreaming(ChannelContextFactory factory)
//        {
//            using ChannelContext ctx = factory.Create();
//            DateTime deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(0.1);

//            var call = ctx.Client.ClientStreaming(deadline: deadline);
//            await Task.Delay(1);

//            Assert.CatchAsync<Exception>(async () => await call.RequestStream.WriteAsync(new RequestMessage { Value = 3 }));
//            var exception = Assert.ThrowsAsync<RpcException>(async () => await call.ResponseAsync);
//            Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.DeadlineExceeded));
//        }

//        [Test, Timeout(TestTimeout)]
//        [TestCaseSource(typeof(MultiChannelSource))]
//        public async Task DeadlineExpiredAfterStreaming(ChannelContextFactory factory)
//        {
//            using ChannelContext ctx = factory.Create();
//            DateTime deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(100);

//            var call = ctx.Client.DelayedClientStreaming(deadline: deadline);
//            await call.RequestStream.WriteAsync(new RequestMessage { Value = 1000 });
//            await call.RequestStream.CompleteAsync();

//            var exception = Assert.ThrowsAsync<RpcException>(async () => await call);
//            Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.DeadlineExceeded));
//        }

//        #endregion

//        #region Dispose tests

//        #endregion

//        #region Exceptions forwarding tests

//        //[Test, Timeout(TestTimeout)]
//        //[TestCaseSource(typeof(MultiChannelSource))]
//        //public Task Throw(ChannelContextFactory factory)
//        //{

//        //}

//        //[Test, Timeout(TestTimeout)]
//        //[TestCaseSource(typeof(MultiChannelSource))]
//        //public Task ThrowCanceledException(ChannelContextFactory factory)
//        //{

//        //}

//        //[Test, Timeout(TestTimeout)]
//        //[TestCaseSource(typeof(MultiChannelSource))]
//        //public Task ThrowRpcException(ChannelContextFactory factory)
//        //{

//        //}

//        #endregion
//    }
//}