using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal.Helpers;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class MessageStreamReader<TMessage> : IAsyncStreamReader<TMessage>
    {
        private readonly IAsyncStreamReader<byte[]> _payloadQueue;
        private readonly Marshaller<TMessage> _marshaller;
        private readonly CancellationToken _callCancellationToken;
        private readonly Deadline _deadline;

        public MessageStreamReader(IAsyncStreamReader<byte[]> payloadQueue, Marshaller<TMessage> marshaller, CancellationToken callCancellationToken, Deadline deadline)
        {
            _payloadQueue = payloadQueue;
            _marshaller = marshaller;
            _callCancellationToken = callCancellationToken;
            _deadline = deadline;
        }

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            try
            {
                var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _callCancellationToken, _deadline.Token);
                return await _payloadQueue.MoveNext(combined.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (_deadline.IsExpired)
                {
                    throw new RpcException(new Status(StatusCode.DeadlineExceeded, ""));
                }
                throw new RpcException(Status.DefaultCancelled);
            }
        }

        public TMessage Current => SerializationHelpers.Deserialize(_marshaller, _payloadQueue.Current);
    }

    //internal class PayloadStreamReader<TMessage> : IAsyncStreamReader<TMessage>
    //{
    //    private readonly IAsyncStreamReader<Frame> _payloadQueue;
    //    private readonly Func<DeserializationContext, TMessage> _deserializer;
    //    private readonly CancellationToken _token;

    //    public PayloadStreamReader(IAsyncStreamReader<Frame> payloadQueue, Func<DeserializationContext, TMessage> marshaller, CancellationToken callCancellationToken)
    //    {
    //        _payloadQueue = payloadQueue;
    //        _deserializer = marshaller;
    //        _token = callCancellationToken;
    //    }

    //    public async Task<bool> MoveNext(CancellationToken cancellationToken)
    //    {
    //        try
    //        {
    //            return await _payloadQueue.MoveNext(_token).ConfigureAwait(false);
    //        }
    //        catch (OperationCanceledException)
    //        {
    //            if (_deadline.IsExpired)
    //            {
    //                throw new RpcException(new Status(StatusCode.DeadlineExceeded, ""));
    //            }
    //            throw new RpcException(Status.DefaultCancelled);
    //        }
    //    }

    //    public TMessage Current => _payloadQueue.Current.GetPayload(_deserializer);
    //}
}