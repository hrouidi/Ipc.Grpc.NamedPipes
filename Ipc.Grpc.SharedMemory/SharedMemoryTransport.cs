#nullable enable
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Google.Protobuf;
using Ipc.Grpc.NamedPipes.Internal.Transport;
using Ipc.Grpc.SharedMemory.Serialization;

namespace Ipc.Grpc.SharedMemory
{
    public class SharedMemoryTransport : IDisposable
    {
        private readonly IMemoryOwner<byte> _frameHeaderOwner;
        private readonly Memory<byte> _frameHeaderBytes;
        private readonly PipeStream _pipeStream;
        private readonly SharedMemory _sharedMemory;

        private readonly string _remote;//Debug only

        public SharedMemoryTransport(SharedMemory sharedMemory)
        {
            _sharedMemory = sharedMemory;
            _pipeStream = null;
            _remote = _pipeStream is NamedPipeClientStream ? "Server" : "Client";
            _frameHeaderOwner = MemoryPool<byte>.Shared.Rent(FrameHeader.Size);
            _frameHeaderBytes = _frameHeaderOwner.Memory.Slice(0, FrameHeader.Size);
        }

        public async ValueTask<Message> ReadFrame(CancellationToken token = default)
        {
            int readBytes = await _pipeStream.ReadAsync(_frameHeaderBytes, token)
                                             .ConfigureAwait(false);
            if (readBytes == 0)
                return Message.Eof;

            FrameHeader header = FrameHeader.FromSpan(_frameHeaderBytes.Span);

            IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(header.TotalSize);
            Memory<byte> messageBytes = owner.Memory.Slice(0, header.TotalSize);

            readBytes = await _pipeStream.ReadAsync(messageBytes, token)
                                         .ConfigureAwait(false);
            if (readBytes == 0)
                return Message.Eof;

            if (readBytes != header.TotalSize)
            {
                Debug.Assert(readBytes == header.TotalSize, $"{_remote}  is laying: read bytes count:{readBytes}/{header.TotalSize}: buffer= {messageBytes.ToArray().Select(x => x.ToString()).Aggregate("", (x, y) => $"{x}|{y}")}");
            }

            Debug.Assert(_pipeStream.IsMessageComplete, "Unexpected message :too long!");

            Memory<byte> payloadBytes = messageBytes.Slice(header.MessageSize);

            Message message = new(payloadBytes, owner);
            message.MergeFrom(messageBytes.Span.Slice(0, header.MessageSize));
            return message;
        }

        public async ValueTask SendFrame<TPayload>(MessageInfo<TPayload> message, CancellationToken token = default) where TPayload : class
        {
            using MemorySerializationContext serializationContext = new(message.Message);
            message.PayloadSerializer(message.Payload, serializationContext);

            Memory<byte> frameBytes = serializationContext.Bytes;

            Memory<byte> headerBytes = frameBytes.Slice(0, FrameHeader.Size);
            FrameHeader.Write(headerBytes.Span, serializationContext.MessageSize, serializationContext.PayloadSize);

            await _pipeStream.WriteAsync(frameBytes, token).ConfigureAwait(false);
        }

        public async ValueTask SendFrame(Message message, CancellationToken token = default)
        {
            var msgSize = message.CalculateSize();

            using var memoryOwner = MemoryPool<byte>.Shared.Rent(FrameHeader.Size + msgSize);
            Memory<byte> frameBytes = memoryOwner.Memory.Slice(0, FrameHeader.Size + msgSize);
            //#1 : frame header
            Memory<byte> headerBytes = frameBytes.Slice(0, FrameHeader.Size);
            FrameHeader.Write(headerBytes.Span, msgSize, 0);
            //#2 : Message
            Memory<byte> messageBytes = frameBytes.Slice(FrameHeader.Size);
            message.WriteTo(messageBytes.Span);

            await _pipeStream.WriteAsync(frameBytes, token).ConfigureAwait(false);
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
}