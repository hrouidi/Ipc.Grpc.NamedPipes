using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal.Helpers;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class MessageChannel
    {
        private sealed class ItemInfo
        {
            public static readonly ItemInfo Completed = new() { IsCompleted = true };

            private ItemInfo() { }

            public ItemInfo(Frame item) => Item = item;

            public ItemInfo(Exception error) => Error = error;

            public Frame Item { get; set; }
            public Exception Error { get; set; }
            public bool IsCompleted { get; private set; }
        }

        private readonly Channel<ItemInfo> _channel;
        private readonly CancellationToken _connectionCancellationToken;
        
        public MessageChannel(CancellationToken connectionCancellationToken)
        {
            _connectionCancellationToken = connectionCancellationToken;
            //_channel = Channel.CreateUnbounded<ItemInfo>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
            _channel = Channel.CreateUnbounded<ItemInfo>();
        }


        public ValueTask Append(Frame message)
        {
            return _channel.Writer.WriteAsync(new ItemInfo(message));
        }

        public ValueTask SetCompleted()
        {
            return _channel.Writer.WriteAsync(ItemInfo.Completed);
        }

        public ValueTask SetError(Exception ex)
        {
            return _channel.Writer.WriteAsync(new ItemInfo(ex));
        }


        public async ValueTask<TPayload> ReadAsync<TPayload>(Deadline deadline, Func<DeserializationContext, TPayload> deserializer) where TPayload : class
        {
            ItemInfo ret = await SafeReadAsync(deadline,_connectionCancellationToken).ConfigureAwait(false); ;
            
            if (ret.Error != null)
                throw MapException(ret.Error, deadline);
            
            if (ret.IsCompleted)
                throw new Exception("Channel completed");

            using Frame msg = ret.Item; // Dispose underlying memory
            TPayload payload = msg.GetPayload(deserializer);
            return payload;
        }

        public IAsyncStreamReader<TPayload> GetAsyncStreamReader<TPayload>(Deadline deadline, Func<DeserializationContext, TPayload> deserializer)
        {
            return new AsyncStreamReaderImplementation<TPayload>(this, deadline, deserializer);
        }


        private async ValueTask<ItemInfo> SafeReadAsync(Deadline deadline,CancellationToken cancellationToken)
        {
            try
            {
                return await _channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false); ;
            }
            catch (Exception e)
            {
                throw MapException(e, deadline);
            }
        }

        public static Exception MapException(Exception ex, Deadline deadline)
        {
            return ex switch
            {
                TimeoutException or IOException => new RpcException(new Status(StatusCode.Unavailable, ex.Message)),
                OperationCanceledException when deadline.IsExpired => new RpcException(new Status(StatusCode.DeadlineExceeded, "")),
                OperationCanceledException => new RpcException(Status.DefaultCancelled),
                _ => ex
            };
        }

        internal sealed class AsyncStreamReaderImplementation<TPayload> : IAsyncStreamReader<TPayload>
        {
            private readonly MessageChannel _messageChannel;
            private readonly Func<DeserializationContext, TPayload> _deserializer;
            private readonly Deadline _deadline;
            internal AsyncStreamReaderImplementation(MessageChannel messageChannel, Deadline deadline, Func<DeserializationContext, TPayload> deserializer)
            {
                _messageChannel = messageChannel;
                _deadline = deadline;
                _deserializer = deserializer;
            }

            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                ItemInfo ret = await _messageChannel.SafeReadAsync(_deadline,cancellationToken).ConfigureAwait(false);

                if (ret.Error != null)
                    throw MapException(ret.Error, _deadline);

                if (ret.IsCompleted)
                    return false;

                using Frame msg = ret.Item; // Dispose underlying memory
                Current = msg.GetPayload(_deserializer);
                return true;
            }

            public TPayload Current { get; private set; }
        }
    }
}