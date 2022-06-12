using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal.Helpers;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class MessageChannel<TPayload> : IAsyncStreamReader<TPayload>
    {
        public sealed class ItemInfo
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
        private readonly Func<DeserializationContext, TPayload> _deserializer;
        private readonly Deadline _deadline;
        private readonly CancellationToken _connectionCancellationToken;
        public MessageChannel(Func<DeserializationContext, TPayload> deserializer, Deadline deadline, CancellationToken connectionCancellationToken)
        {
            _deserializer = deserializer;
            _deadline = deadline;
            _connectionCancellationToken = connectionCancellationToken;
            _channel = Channel.CreateUnbounded<ItemInfo>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
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

        public async ValueTask<TPayload> ReadAsync()
        {
            ItemInfo ret = await SafeReadAsync(_connectionCancellationToken).ConfigureAwait(false); ;
            if (ret.Error != null)
                throw MapException(ret.Error);
            if (ret.IsCompleted)
                throw new Exception("Channel completed");

            using Frame msg = ret.Item; // Dispose underlying memory
            TPayload payload = msg.GetPayload(_deserializer);
            return payload;
        }



        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            while (_channel.Reader.Completion.IsCompleted == false)
            {
                ItemInfo ret = await SafeReadAsync(cancellationToken).ConfigureAwait(false);
                
                if (ret.Error != null)
                    throw MapException(ret.Error);
                
                if (ret.IsCompleted)
                    return false;

                using Frame msg = ret.Item; // Dispose underlying memory
                Current = msg.GetPayload(_deserializer);
                return true;
            }
            return false;
        }

        public TPayload Current { get; private set; }
        
        private async ValueTask<ItemInfo> SafeReadAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false); ;
            }
            catch (Exception e)
            {
                throw MapException(e);
            }
        }

        private Exception MapException(Exception ex)
        {
            return ex switch
            {
                TimeoutException or IOException => new RpcException(new Status(StatusCode.Unavailable, ex.Message)),
                OperationCanceledException when _deadline.IsExpired => new RpcException(new Status(StatusCode.DeadlineExceeded, "")),
                OperationCanceledException => new RpcException(Status.DefaultCancelled),
                _ => ex
            };
        }

    }
}