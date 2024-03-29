using System.Threading.Tasks;
using ProtoBuf.Grpc;

namespace Ipc.Grpc.NamedPipes.Tests.ProtoContract
{
    public class TestServiceImplementation : ITestService
    {
        //#region Unary
        public bool SimplyUnaryCalled { get; private set; }
        public async ValueTask<ResponseMessage> SimpleUnaryAsync(RequestMessage request, CallContext context = default)
        {
            await Task.Yield();
            SimplyUnaryCalled = true;
            return new ResponseMessage
            {
                Value = request.Value,
                Binary = request.Binary
            };
        }

        public async ValueTask<ResponseMessage> TestAsync(ProtoMessage request, CallContext context = default)
        {
            await Task.Yield();
            return new ResponseMessage
            {
                Value = (int)request.Kind,
            };
        }

        //public override async Task<ResponseMessage> DelayedUnary(RequestMessage request, ServerCallContext context)
        //{
        //    await Task.Delay(request.Value, context.CancellationToken);
        //    return new ResponseMessage();
        //}

        //public override Task<ResponseMessage> ThrowingUnary(RequestMessage request, ServerCallContext context)
        //{
        //    throw ExceptionToThrow;
        //}

        //public override async Task<ResponseMessage> DelayedThrowingUnary(RequestMessage request, ServerCallContext context)
        //{
        //    await Task.Delay(2000, context.CancellationToken);
        //    throw ExceptionToThrow;
        //}

        //public override async Task<ResponseMessage> UnarySetHeadersTrailers(RequestMessage request, ServerCallContext context)
        //{
        //    RequestHeaders = context.RequestHeaders;
        //    await context.WriteResponseHeadersAsync(ResponseHeaders);
        //    foreach (Metadata.Entry entry in ResponseTrailers)
        //    {
        //        if (entry.IsBinary)
        //            context.ResponseTrailers.Add(entry.Key, entry.ValueBytes);
        //        else
        //            context.ResponseTrailers.Add(entry.Key, entry.Value);
        //    }
        //    return new ResponseMessage
        //           {
        //               Value = request.Value,
        //               Binary = request.Binary
        //           };
        //}

        //public override Task<ResponseMessage> UnarySetStatus(RequestMessage request, ServerCallContext context)
        //{
        //    context.Status = new Status(StatusCode.InvalidArgument, "invalid argument");
        //    return Task.FromResult(new ResponseMessage());
        //}

        //#endregion

        //#region Client streaming

        //public override async Task<ResponseMessage> ClientStreaming(IAsyncStreamReader<RequestMessage> requestStream, ServerCallContext context)
        //{
        //    int total = 0;
        //    while (await requestStream.MoveNext())
        //    {
        //        total += requestStream.Current.Value;
        //    }

        //    return new ResponseMessage { Value = total };
        //}

        //public override async Task<ResponseMessage> DelayedClientStreaming(IAsyncStreamReader<RequestMessage> requestStream, ServerCallContext context)
        //{
        //    int total = 0;
        //    while (await requestStream.MoveNext())
        //    {
        //        total += requestStream.Current.Value;
        //    }
        //    await Task.Delay(total, context.CancellationToken);
        //    return new ResponseMessage { Value = total };
        //}

        //#endregion

        //#region Server streaming

        //public override async Task ServerStreaming(RequestMessage request, IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
        //{
        //    ServerStream = responseStream;
        //    for (int i = request.Value; i > 0; i--)
        //    {
        //        await responseStream.WriteAsync(new ResponseMessage { Value = i});
        //    }
        //}

        //public override async Task DelayedServerStreaming(RequestMessage request, IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
        //{
        //    for (int i = request.Value; i > 0; i--)
        //    {
        //        await responseStream.WriteAsync(new ResponseMessage { Value = i });
        //        await Task.Delay(2000, context.CancellationToken);
        //        if (context.CancellationToken.IsCancellationRequested)
        //        {
        //            break;
        //        }
        //    }
        //}

        //public override async Task ThrowingServerStreaming(RequestMessage request, IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
        //{
        //    ServerStream = responseStream;
        //    for (int i = request.Value; i > 0; i--)
        //    {
        //        await responseStream.WriteAsync(new ResponseMessage { Value = i });
        //    }
        //    throw new Exception("blah");
        //}

        //#endregion

        //#region Duplex streaming

        //public override async Task DuplexStreaming(IAsyncStreamReader<RequestMessage> requestStream, IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
        //{
        //    await responseStream.WriteAsync(new ResponseMessage { Value = 10 });
        //    await responseStream.WriteAsync(new ResponseMessage { Value = 11 });
        //    await Task.Delay(100);
        //    while (await requestStream.MoveNext())
        //    {
        //        await responseStream.WriteAsync(new ResponseMessage { Value = requestStream.Current.Value });
        //    }
        //}

        //public override async Task DelayedDuplexStreaming(IAsyncStreamReader<RequestMessage> requestStream, IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
        //{
        //    while (await requestStream.MoveNext(context.CancellationToken))
        //    {
        //        await responseStream.WriteAsync(new ResponseMessage { Value = requestStream.Current.Value });
        //        await Task.Delay(2000, context.CancellationToken);
        //    }
        //}

        //public override async Task ThrowingDuplexStreaming(IAsyncStreamReader<RequestMessage> requestStream, IServerStreamWriter<ResponseMessage> responseStream, ServerCallContext context)
        //{
        //    while (await requestStream.MoveNext())
        //    {
        //        await responseStream.WriteAsync(new ResponseMessage { Value = requestStream.Current.Value });
        //    }
        //    throw new Exception("blah");
        //}

        //#endregion

        //#region Tests heplers
        //public Exception ExceptionToThrow { get; set; } = new InvalidOperationException("Test exception");



        //public IServerStreamWriter<ResponseMessage> ServerStream { get; private set; }

        //public Metadata RequestHeaders { get; private set; }

        //public Metadata ResponseHeaders { private get; set; }

        //public Metadata ResponseTrailers { private get; set; }

        //#endregion
    }
}