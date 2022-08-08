#nullable enable
using System;
using System.Buffers;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Ipc.Grpc.NamedPipes.Internal.Transport
{
    internal class NamedPipeTransport2 : INamedPipeTransport
    {
        private readonly IMemoryOwner<byte> _frameHeaderOwner;
        private readonly PipeWrapper _pipeWrapper;

        private readonly string _remote;//Debug only

        public NamedPipeTransport2(PipeStream pipeStream)
        {
            _pipeWrapper = new PipeWrapper(pipeStream);
            _remote = pipeStream is NamedPipeClientStream ? "Server" : "Client";
            _frameHeaderOwner = MemoryPool<byte>.Shared.Rent(FrameHeader.Size);
            _frameHeaderOwner.Memory.Slice(0, FrameHeader.Size);
        }

        public async ValueTask<Message> ReadFrame(CancellationToken token = default)
        {
            (Guid guid, int size) frame = await _pipeWrapper.ReadAsync(token).ConfigureAwait(false);
            if (frame == default)
                return Message.Eof;

            return FakeRead(frame.guid, frame.size);

            static Message Read(Guid guid, int size)
            {
                MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(guid.ToString());
                MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(0, size);
                Span<byte> bytes = accessor.GetSpan();
                FrameHeader header = FrameHeader.FromSpan(bytes.Slice(0, FrameHeader.Size));
                Message ret = new(mmf, FrameHeader.Size + header.MessageSize, header.PayloadSize);
                ret.MergeFrom(bytes.Slice(FrameHeader.Size, header.MessageSize));
                return ret;
            }
            static Message FakeRead(Guid guid, int size)
            {
                return new Message();
            }
        }

        public async ValueTask SendFrame<TPayload>(MessageInfo<TPayload> message, CancellationToken token = default) where TPayload : class
        {
            SharedMemorySerializationContext serializationContext = new(message.Message);
            message.PayloadSerializer(message.Payload, serializationContext);
            await _pipeWrapper.WriteAsync(serializationContext.MapName, serializationContext.GlobalSize, token).ConfigureAwait(false);
        }

        public async ValueTask SendFrame(Message message, CancellationToken token = default)
        {
            (Guid guid, int size) = FakeWrite(message);
            await _pipeWrapper.WriteAsync(guid, size, token).ConfigureAwait(false);

            static (Guid guid, int size) Write(IMessage message)
            {
                int messageSize = message.CalculateSize();
                var mapName = Guid.NewGuid();
                int globalSize = FrameHeader.Size + messageSize;
                var ret = MemoryMappedFile.CreateNew(mapName.ToString(), globalSize);
                using MemoryMappedViewAccessor accessor = ret.CreateViewAccessor(0, globalSize);
                Span<byte> bytes = accessor.GetSpan();
                //#1 : header
                int padding = NamedPipeTransport.FrameHeader.Size;
                Span<byte> headerBytes = bytes.Slice(0, padding);
                NamedPipeTransport2.FrameHeader.Write(headerBytes, messageSize, 0);
                //#2 : Message
                Span<byte> messageBytes = bytes.Slice(padding, messageSize);
                message.WriteTo(messageBytes);
                return (mapName, globalSize);
            }

            static (Guid guid, int size) FakeWrite(IMessage message)
            {
                int messageSize = message.CalculateSize();
                var mapName = Guid.NewGuid();
                int globalSize = FrameHeader.Size + messageSize;
                return (mapName, globalSize);
            }
        }

        public void Dispose()
        {
            _frameHeaderOwner.Dispose();
        }

        [StructLayout(LayoutKind.Sequential, Size = Size)]
        internal readonly struct FrameHeader : IEquatable<FrameHeader>
        {
            public const int Size = 2 * sizeof(int);

            public FrameHeader(int messageSize, int payloadSize)
            {
                PayloadSize = payloadSize;
                MessageSize = messageSize;
            }

            public int MessageSize { get; }

            public int PayloadSize { get; }

            public int TotalSize => MessageSize + PayloadSize;

            public static FrameHeader FromSpan(ReadOnlySpan<byte> span)
            {
                return MemoryMarshal.Read<FrameHeader>(span);
            }

            public static void Write(Span<byte> destination, int messageSize, int payloadSize)
            {
                long bytes = messageSize + ((long)payloadSize << 32);
                MemoryMarshal.Write(destination, ref bytes);
            }

            #region Equality 

            public override int GetHashCode()
            {
                unchecked
                {
                    return (MessageSize * 397) ^ PayloadSize;
                }
            }

            public bool Equals(FrameHeader other) => PayloadSize == other.PayloadSize && MessageSize == other.MessageSize;

            public override bool Equals(object? obj) => obj is FrameHeader other && Equals(other);

            public static bool operator ==(FrameHeader left, FrameHeader right) => left.Equals(right);

            public static bool operator !=(FrameHeader left, FrameHeader right) => !left.Equals(right);

            #endregion

            public override string ToString() => $"[{nameof(TotalSize)} = {TotalSize}],[{nameof(MessageSize)} ={MessageSize}],[{nameof(PayloadSize)} ={PayloadSize}]";
        }
    }

    public class PipeWrapper
    {
        private readonly PipeStream _pipeStream;

        public PipeWrapper(PipeStream pipeStream)
        {
            _pipeStream = pipeStream;
        }

        public async ValueTask WriteAsync(Guid guid, int size, CancellationToken token = default)
        {
            byte[] tmp = Write(guid, size);
            await _pipeStream.WriteAsync(tmp, 0, tmp.Length, token);
        }

        public async ValueTask<(Guid guid, int size)> ReadAsync(CancellationToken token = default)
        {
            byte[] tmp = new byte[20];
            int readAsync = await _pipeStream.ReadAsync(tmp, 0, tmp.Length, token);
            if (readAsync == 0)
                return default;
            return Read(tmp);
        }

        public void WriteSync(Guid guid, int size)
        {
            var tmp = Write(guid, size);
            _pipeStream.Write(tmp, 0, tmp.Length);
        }

        public (Guid guid, int size) ReadSync()
        {
            byte[] tmp = new byte[20];
            int readAsync = _pipeStream.Read(tmp, 0, tmp.Length);
            return Read(tmp);
        }

        public byte[] Write(Guid guid, int size)
        {
            byte[] tmp = new byte[20];
            MemoryMarshal.Write(tmp, ref guid);
            MemoryMarshal.Write(tmp.AsSpan().Slice(16), ref size);
            return tmp;
        }

        public (Guid guid, int size) Read(byte[] buffer)
        {
            Span<byte> tmp = buffer.AsSpan();
            //Guid guid = new(tmp.Slice(0, 16));
            Guid guid = MemoryMarshal.Read<Guid>(tmp.Slice(0, 16));
            int size = MemoryMarshal.Read<int>(tmp.Slice(16));
            return (guid, size);
        }

    }
}