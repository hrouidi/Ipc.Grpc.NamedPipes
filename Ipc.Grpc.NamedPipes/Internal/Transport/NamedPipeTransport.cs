#nullable enable
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Ipc.Grpc.NamedPipes.Internal.Helpers;

namespace Ipc.Grpc.NamedPipes.Internal.Transport
{
    internal class NamedPipeTransport : IDisposable
    {
        private readonly byte[] _frameHeaderBytes;
        private readonly PipeStream _pipeStream;

        private readonly string _remote;//Debug only

        public NamedPipeTransport(PipeStream pipeStream)
        {
            _pipeStream = pipeStream;
            _remote = pipeStream is NamedPipeClientStream ? "Server" : "Client";
            _frameHeaderBytes = ArrayPool<byte>.Shared.Rent(FrameHeader.Size);
        }

        public async ValueTask<Frame> ReadFrame(CancellationToken token = default)
        {
            int readBytes = await _pipeStream.ReadAsync(_frameHeaderBytes, 0, FrameHeader.Size, token)
                                             .ConfigureAwait(false);
            if (readBytes == 0)
                return Frame.Eof;

            FrameHeader header = FrameHeader.FromSpan(_frameHeaderBytes.AsSpan().Slice(0, FrameHeader.Size));

            //int framePlusPayloadSize = header.PayloadSize + header.MessageSize;
            IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(header.TotalSize);
            Memory<byte> framePlusPayloadBytes = owner.Memory.Slice(0, header.TotalSize);

            readBytes = await _pipeStream.ReadAsync(framePlusPayloadBytes, token)
                                         .ConfigureAwait(false);

            Debug.Assert(readBytes == header.TotalSize, $"{_remote}  is a layer !");
            Debug.Assert(_pipeStream.IsMessageComplete, "Unexpected message :too long!");


            Message? message = Message.Parser.ParseFrom(framePlusPayloadBytes.Span.Slice(0, header.MessageSize));
            Memory<byte> payloadBytes = framePlusPayloadBytes.Slice(header.MessageSize);

            //Message message = new(payloadBytes, owner);
            //message.MergeFrom(framePlusPayloadBytes.Span.Slice(0, header.MessageSize));

            Frame packet = new(message, payloadBytes, owner);
            return packet;
        }

        public ValueTask SendFrame<TPayload>(FrameInfo<TPayload> frame, CancellationToken token = default) where TPayload : class
        {
            using MemorySerializationContext serializationContext = new(frame.Message);
            frame.PayloadSerializer(frame.Payload, serializationContext);

            Memory<byte> frameBytes = serializationContext.Bytes;

            Memory<byte> headerBytes = frameBytes.Slice(0, FrameHeader.Size);
            FrameHeader.Write(headerBytes.Span, serializationContext.MessageSize, serializationContext.PayloadSize);

            return _pipeStream.WriteAsync(frameBytes, token);
        }

        public ValueTask SendFrame(Message message, CancellationToken token = default)
        {
            var msgSize = message.CalculateSize();

            using var memoryOwner = MemoryPool<byte>.Shared.Rent(FrameHeader.Size + msgSize);
            Memory<byte> frameBytes = memoryOwner.Memory.Slice(0, FrameHeader.Size + msgSize);
            //#1 : frame header
            Memory<byte> headerBytes = frameBytes.Slice(0, FrameHeader.Size);
            FrameHeader.Write(headerBytes.Span, msgSize,0);
            //#2 : Message
            Memory<byte> messageBytes = frameBytes.Slice(FrameHeader.Size);
            MessageExtensions.WriteTo((IMessage)message, messageBytes.Span);

            return _pipeStream.WriteAsync(frameBytes, token);
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_frameHeaderBytes);
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