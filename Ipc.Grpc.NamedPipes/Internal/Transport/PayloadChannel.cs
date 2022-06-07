using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Grpc.Core;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class PayloadChannel<TItem> : IAsyncStreamReader<TItem>
    {
        public sealed class ItemInfo
        {
            public static readonly ItemInfo Completed = new() { IsCompleted = true };

            private ItemInfo() { }

            public ItemInfo(TItem item) => Item = item;

            public ItemInfo(Exception error) => Error = error;

            public TItem Item { get; set; }
            public Exception Error { get; set; }
            public bool IsCompleted { get; private set; }
        }

        private readonly Channel<ItemInfo> _channel;

        public PayloadChannel() => _channel = Channel.CreateUnbounded<ItemInfo>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

        public ValueTask Append(TItem item)
        {
            return _channel.Writer.WriteAsync(new ItemInfo(item));
        }

        public ValueTask SetCompleted()
        {
            return _channel.Writer.WriteAsync(ItemInfo.Completed);
        }

        public ValueTask SetError(Exception ex)
        {
            return _channel.Writer.WriteAsync(new ItemInfo(ex));
        }

        public async ValueTask<TItem> ReadAsync(CancellationToken cancellationToken)
        {
            ItemInfo ret = await _channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false); ;
            if (ret.Error != null)
                throw ret.Error;
            if (ret.IsCompleted)
                throw new Exception("Channel completed");
            return ret.Item;

        }

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            while (_channel.Reader.Completion.IsCompleted == false)
            {
                ItemInfo ret = await _channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (ret.Error != null)
                    throw ret.Error;
                if (ret.IsCompleted)
                    return false;
                Current = ret.Item;
                return true;
            }
            return false;
        }

        public TItem Current { get; private set; }

    }
}