using System.Threading.Channels;

namespace Ipc.Grpc.SharedMemory
{

    public class MmfChannelWriter<TPayload> : ChannelWriter<TPayload>
    {
        public override bool TryWrite(TPayload item)
        {
            throw new NotImplementedException();
        }

        public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }
    }

    public class MmfChannelReader<TPayload> : ChannelReader<TPayload>
    {
        public override bool TryRead(out TPayload item)
        {

            throw new NotImplementedException();
        }

        public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }
    }

    public class MMfChannel<TPayload> : Channel<TPayload>
    {

    }
}