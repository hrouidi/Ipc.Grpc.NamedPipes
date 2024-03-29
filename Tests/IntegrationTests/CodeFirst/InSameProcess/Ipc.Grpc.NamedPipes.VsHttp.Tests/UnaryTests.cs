using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Tests.ProtoContract;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.CaseSources;
using Ipc.Grpc.NamedPipes.VsHttp.Tests.Helpers;
using NUnit.Framework;

namespace Ipc.Grpc.NamedPipes.VsHttp.Tests
{
    public class UnaryTests
    {
        public const int TestTimeout = 3000;

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        //public async Task AsyncCallTest(ChannelContextFactory factory)
        public async Task AsyncCallTest(NamedPipeChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            ResponseMessage response = await ctx.Client.SimpleUnaryAsync(new RequestMessage { Value = 123 });
            Assert.That(response.Value, Is.EqualTo(123));
            Assert.True(ctx.Impl.SimplyUnaryCalled);
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task LargePayloadTest(ChannelContextFactory factory)
        {
            var bytes = new byte[1024 * 1024];
            new Random(1234).NextBytes(bytes);

            //ByteString byteString = UnsafeByteOperations.UnsafeWrap(bytes);

            using ChannelContext ctx = factory.Create();
            ResponseMessage response = await ctx.Client.SimpleUnaryAsync(new RequestMessage { Binary = bytes });
            Assert.That(response.Binary, Is.EqualTo(bytes));
        }

        //[Test, Timeout(TestTimeout)]
        //[TestCaseSource(typeof(MultiChannelSource))]
        //public Task SetStatus(ChannelContextFactory factory)
        //{
        //    using ChannelContext ctx = factory.Create();
        //    var call = ctx.Client.UnarySetStatusAsync(new RequestMessage());
        //    var exception = Assert.ThrowsAsync<RpcException>(async () => await call);
        //    Assert.That(exception.Status.StatusCode, Is.EqualTo(StatusCode.InvalidArgument));
        //    Assert.That(exception.Status.Detail, Is.EqualTo("invalid argument"));
        //    return Task.CompletedTask;
        //}

        //[Test, Timeout(TestTimeout)]
        //[TestCaseSource(typeof(MultiChannelSource))]
        //public async Task SetHeadersAndTrailers(ChannelContextFactory factory)
        //{
        //    using ChannelContext ctx = factory.Create();
        //    var requestHeaders = new Metadata
        //    {
        //        {"A1", "1"},
        //        {"A2-bin", new[] {(byte) 2}},
        //    };
        //    var responseHeaders = new Metadata
        //    {
        //        {"B1", "1"},
        //        {"B2-bin", new[] {(byte) 2}},
        //    };
        //    var responseTrailers = new Metadata
        //    {
        //        {"C1", "1"},
        //        {"C2-bin", new[] {(byte) 2}},
        //    };

        //    ctx.Impl.ResponseHeaders = responseHeaders;
        //    ctx.Impl.ResponseTrailers = responseTrailers;
        //    AsyncUnaryCall<ResponseMessage> call = ctx.Client.UnarySetHeadersTrailersAsync(new RequestMessage { Value = 1 }, requestHeaders);

        //    Metadata actualResponseHeaders = await call.ResponseHeadersAsync;
        //    await call.ResponseAsync;
        //    Metadata actualResponseTrailers = call.GetTrailers();
        //    Status actualStatus = call.GetStatus();
        //    Metadata actualRequestHeaders = ctx.Impl.RequestHeaders;

        //    MetadataAssert.AreEquivalent(requestHeaders, actualRequestHeaders);
        //    MetadataAssert.AreEquivalent(responseHeaders, actualResponseHeaders);
        //    MetadataAssert.AreEquivalent(responseTrailers, actualResponseTrailers);
        //    Assert.That(actualStatus.StatusCode, Is.EqualTo(StatusCode.OK));
        //}

        #region Cancel tests

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public Task CancelBeforeCall(ChannelContextFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var responseTask = ctx.Client.SimpleUnaryAsync(new RequestMessage { Value = 10 }, cts.Token);
            var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
            Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.Cancelled));
            Assert.False(ctx.Impl.SimplyUnaryCalled);
            return Task.CompletedTask;
        }

        //[Test, Timeout(TestTimeout)]
        //[TestCaseSource(typeof(MultiChannelSource))]
        //public Task CancelBeforeWaitingResponse(ChannelContextFactory factory)
        //{
        //    using ChannelContext ctx = factory.Create();
        //    var cts = new CancellationTokenSource();
        //    var responseTask = ctx.Client.DelayedUnaryAsync(new RequestMessage { Value = 10 }, cancellationToken: cts.Token);
        //    cts.Cancel();
        //    var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
        //    Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.Cancelled));
        //    return Task.CompletedTask;
        //}

        //[Test, Timeout(TestTimeout)]
        //[TestCaseSource(typeof(MultiChannelSource))]
        //public Task CancelBeforeServiceFault(ChannelContextFactory factory)
        //{
        //    using ChannelContext ctx = factory.Create();
        //    var cts = new CancellationTokenSource();
        //    var responseTask = ctx.Client.DelayedThrowingUnaryAsync(new RequestMessage { Value = 10 }, cancellationToken: cts.Token);
        //    cts.Cancel();
        //    var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
        //    Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.Cancelled));
        //    return Task.CompletedTask;
        //}

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public async Task CancellationRaceTest(ChannelContextFactory factory)
        {
            //TODO: Fix me
            using ChannelContext ctx = factory.Create();
            var random = new Random();
            var bytes = new byte[1024 * 1024];
            random.NextBytes(bytes);
            // ByteString byteString = UnsafeByteOperations.UnsafeWrap(bytes);
            var request = new RequestMessage { Binary = bytes };

            var averageExecutionTime = (int)await MeasureCallTime();
            Assert.IsTrue(averageExecutionTime > 0, "test is not conclusive");
            int iterationCount = averageExecutionTime * 10;
            for (int i = 0; i < iterationCount; i++)
            {
                var cts = new CancellationTokenSource(random.Next(averageExecutionTime));
                try
                {
                    ResponseMessage response = await ctx.Client.SimpleUnaryAsync(request, cts.Token);
                }
                catch (RpcException ex)
                {
                    Assert.That(ex.StatusCode, Is.EqualTo(StatusCode.Cancelled));
                }
            }

            async Task<long> MeasureCallTime()
            {
                var stopwatch = Stopwatch.StartNew();
                ResponseMessage response = await ctx.Client.SimpleUnaryAsync(request, CancellationToken.None);
                stopwatch.Stop();
                Assert.That(response.Binary, Is.EqualTo(bytes));
                Console.WriteLine($"Average execution time for {nameof(ctx.Client.SimpleUnaryAsync)} : {stopwatch.ElapsedMilliseconds} ms ");
                return stopwatch.ElapsedMilliseconds;
            }
        }

        #endregion

        #region Dispose tests

        //[Test, Timeout(TestTimeout)]
        //[TestCaseSource(typeof(MultiChannelSource))]
        //public async Task Dispose_ManyTimes_Test(ChannelContextFactory factory)
        //{
        //    using ChannelContext ctx = factory.Create();
        //    var responseTask = ctx.Client.SimpleUnaryAsync(new RequestMessage { Value = 10 });
        //    ResponseMessage response = await responseTask;
        //    responseTask.Dispose();
        //    responseTask.Dispose();
        //    responseTask.Dispose();
        //    Assert.That(response.Value, Is.EqualTo(10));

        //    responseTask = ctx.Client.DelayedUnaryAsync(new RequestMessage { Value = 10 });
        //    responseTask.Dispose();
        //    responseTask.Dispose();
        //    responseTask.Dispose();
        //    var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
        //    Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.Cancelled));
        //}

        //[Test, Timeout(TestTimeout)]
        //[TestCaseSource(typeof(MultiChannelSource))]
        //public Task DisposeBeforeAwaiting_ShouldThrowCanceledRpcException_Test(ChannelContextFactory factory)
        //{
        //    using ChannelContext ctx = factory.Create();
        //    var responseTask = ctx.Client.DelayedUnaryAsync(new RequestMessage { Value = 10 });
        //    responseTask.Dispose();
        //    var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
        //    Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.Cancelled));
        //    return Task.CompletedTask;
        //}

        //[Test, Timeout(TestTimeout)]
        //[TestCaseSource(typeof(MultiChannelSource))]
        //public async Task DisposeAfterAwaiting_ShouldDoNothing_Test(ChannelContextFactory factory)
        //{
        //    using ChannelContext ctx = factory.Create();
        //    var responseTask = ctx.Client.SimpleUnaryAsync(new RequestMessage { Value = 10 });
        //    ResponseMessage response = await responseTask.ResponseAsync;
        //    responseTask.Dispose();
        //    Assert.That(response.Value, Is.EqualTo(10));
        //}

        //[Test, Timeout(TestTimeout)]
        //[TestCaseSource(typeof(NamedPipeChannelSource))]
        //public void DisposeWhileAwaiting_ShouldSendCancelRemoteAndThrowCanceledRpcException_Test(ChannelContextFactory factory)
        //{
        //    using ChannelContext ctx = factory.Create();
        //    var call = ctx.Client.DelayedUnaryAsync(new RequestMessage { Value = 100 }); //wait for 100 ms

        //    Task task = Task.WhenAll(call.ResponseAsync, DisposeAfter(call, 50));

        //    var exception = Assert.ThrowsAsync<RpcException>(async () => await task);
        //    Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.Cancelled));

        //    static async Task DisposeAfter(IDisposable call, int ms)
        //    {
        //        await Task.Delay(ms);
        //        call.Dispose();
        //    }
        //}

        #endregion

        #region Deadline tests

        //[Test, Timeout(TestTimeout)]
        //[TestCaseSource(typeof(MultiChannelSource))]
        //public Task Deadline(ChannelContextFactory factory)
        //{
        //    using ChannelContext ctx = factory.Create();
        //    DateTime deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(1);
        //    var call = ctx.Client.DelayedUnaryAsync(new RequestMessage { Value = 1000 }, deadline: deadline);
        //    var exception = Assert.ThrowsAsync<RpcException>(async () => await call);
        //    Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.DeadlineExceeded));
        //    return Task.CompletedTask;
        //}

        //[Test, Timeout(TestTimeout)]
        //[TestCaseSource(typeof(MultiChannelSource))]
        //public Task AlreadyExpiredDeadline(ChannelContextFactory factory)
        //{
        //    using ChannelContext ctx = factory.Create();
        //    DateTime deadline = DateTime.UtcNow - TimeSpan.FromSeconds(0.1);
        //    var call = ctx.Client.SimpleUnaryAsync(new RequestMessage(), deadline: deadline);
        //    var exception = Assert.ThrowsAsync<RpcException>(async () => await call);
        //    Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.DeadlineExceeded));
        //    Assert.False(ctx.Impl.SimplyUnaryCalled);
        //    return Task.CompletedTask;
        //}

        #endregion

        #region Exceptions forwarding tests

        //[Test, Timeout(TestTimeout)]
        //[TestCaseSource(typeof(MultiChannelSource))]
        //public Task ThrowingUnary(ChannelContextFactory factory)
        //{
        //    using ChannelContext ctx = factory.Create();
        //    var responseTask = ctx.Client.ThrowingUnaryAsync(new RequestMessage { Value = 10 });
        //    var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
        //    Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Unknown));
        //    StringAssert.StartsWith("Exception was thrown by handler", exception.Status.Detail);
        //    return Task.CompletedTask;
        //}

        //[Test, Timeout(TestTimeout)]
        //[TestCaseSource(typeof(MultiChannelSource))]
        //public Task ThrowCanceledExceptionUnary(ChannelContextFactory factory)
        //{
        //    using ChannelContext ctx = factory.Create();
        //    ctx.Impl.ExceptionToThrow = new OperationCanceledException();
        //    var responseTask = ctx.Client.ThrowingUnaryAsync(new RequestMessage { Value = 10 });
        //    var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
        //    Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Unknown));
        //    StringAssert.StartsWith("Exception was thrown by handler", exception.Status.Detail);
        //    return Task.CompletedTask;
        //}

        //[Test, Timeout(TestTimeout)]
        //[TestCaseSource(typeof(MultiChannelSource))]
        //public Task ThrowRpcExceptionUnary(ChannelContextFactory factory)
        //{
        //    using ChannelContext ctx = factory.Create();
        //    ctx.Impl.ExceptionToThrow = new RpcException(new Status(StatusCode.InvalidArgument, "Bad arg"));
        //    var responseTask = ctx.Client.ThrowingUnaryAsync(new RequestMessage { Value = 10 });
        //    var exception = Assert.ThrowsAsync<RpcException>(async () => await responseTask);
        //    Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.InvalidArgument));
        //    Assert.That(exception.Status.Detail, Is.EqualTo("Bad arg"));
        //    return Task.CompletedTask;
        //}

        #endregion

        //TODO:  move to separate channel/server tests

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(MultiChannelSource))]
        public void ConnectionTimeout(ChannelContextFactory factory)
        {
            var client = factory.CreateClient();
            var exception = Assert.ThrowsAsync<RpcException>(async () => await client.SimpleUnaryAsync(new RequestMessage { Value = 10 }));
            Assert.That(exception!.StatusCode, Is.EqualTo(StatusCode.Unavailable));
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(GrpcDotNetNamedPipesChannelSource))]
        public void CallImmediatelyAfterKillingServer(GrpcDotNetNamedPipesChannelFactory factory)
        {
            using ChannelContext ctx = factory.Create();
            ctx.Dispose();
            var exception = Assert.ThrowsAsync<RpcException>(async () => await ctx.Client.SimpleUnaryAsync(new RequestMessage { Value = 10 }));
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCode.Unavailable));
            Assert.That(exception.Status.Detail, Is.EqualTo("failed to connect to all addresses"));
        }

        [Test, Timeout(TestTimeout)]
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

        [Test, Timeout(TestTimeout)]
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

        #region  ACL tests 

#if NET_5_0 || NETFRAMEWORK
        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(NamedPipeChannelSource))]
        public async Task SimpleUnaryWithACLs(NamedPipeChannelContextFactory factory)
        {
            var security = new PipeSecurity();
            var sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.ReadWrite, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User!, PipeAccessRights.FullControl, AccessControlType.Allow));

            var options = new NamedPipeServerOptions { PipeSecurity = security };

            using ChannelContext ctx = factory.Create(options);
            ResponseMessage response = await ctx.Client.SimpleUnaryAsync(new RequestMessage { Value = 10 });
            Assert.That(response.Value, Is.EqualTo(10));
            Assert.True(ctx.Impl.SimplyUnaryCalled);
        }

        [Test, Timeout(TestTimeout)]
        [TestCaseSource(typeof(NamedPipeChannelSource))]
        public void SimpleUnaryWithACLsDenied(NamedPipeChannelContextFactory factory)
        {
            var security = new PipeSecurity();
            var sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.ReadWrite, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User!, PipeAccessRights.ReadWrite, AccessControlType.Deny));

            var options = new NamedPipeServerOptions { PipeSecurity = security };

            Assert.Throws<UnauthorizedAccessException>(() => factory.Create(options));
        }
#endif

        #endregion
    }
}