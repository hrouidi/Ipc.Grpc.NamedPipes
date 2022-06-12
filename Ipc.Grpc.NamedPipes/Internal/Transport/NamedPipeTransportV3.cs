#nullable enable
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Ipc.Grpc.NamedPipes.Internal.Helpers;
using Google.Protobuf;
using Ipc.Grpc.NamedPipes.TransportProtocol;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class NamedPipeTransportV3 : IDisposable
    {
        private readonly byte[] _frameHeaderBytes;
        private readonly PipeStream _pipeStream;

        public NamedPipeTransportV3(PipeStream pipeStream)
        {
            _pipeStream = pipeStream;
            _frameHeaderBytes = ArrayPool<byte>.Shared.Rent(FrameHeader.Size);
        }

        public async ValueTask<Frame> ReadFrame(CancellationToken token = default)
        {
            int readBytes = await _pipeStream.ReadAsync(_frameHeaderBytes, 0, FrameHeader.Size, token)
                                             .ConfigureAwait(false);
            Debug.Assert(readBytes == FrameHeader.Size, "Client does not speak my dialect :/");

            FrameHeader header = FrameHeader.FromSpan(_frameHeaderBytes.AsSpan().Slice(0, FrameHeader.Size));

            IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(header.TotalSize);
            Memory<byte> framePlusPayloadBytes = owner.Memory.Slice(0, header.TotalSize - FrameHeader.Size);

            readBytes = await _pipeStream.ReadAsync(framePlusPayloadBytes, token)
                                         .ConfigureAwait(false);

            Debug.Assert(readBytes == header.TotalSize - FrameHeader.Size, "Client is a layer !");
            Debug.Assert(_pipeStream.IsMessageComplete, "Unexpected message :too long!");


            //Message? message = Message.Parser.ParseFrom(framePlusPayloadBytes.Span.Slice(0, header.MessageSize));
            var payloadBytes = framePlusPayloadBytes.Slice(header.MessageSize);
            
            Message message = new(payloadBytes, owner);
            message.MergeFrom(framePlusPayloadBytes.Span.Slice(0, header.MessageSize));
            //if (header.PayloadSize == 0)
            //{
            //    owner.Dispose();
            //    return (message, null, null);
            //}
            //var payloadBytes = framePlusPayloadBytes.Slice(header.MessageSize);
            var packet = new Frame(message, payloadBytes, owner);
            return packet;
        }

        public ValueTask SendFrame<TPayload>(FrameInfo<TPayload> frame, CancellationToken token = default) where TPayload : class
        {
            using MemorySerializationContext serializationContext = new(frame.Message);
            frame.PayloadSerializer(frame.Payload, serializationContext);

            Memory<byte> bytes = serializationContext.Bytes;

            Memory<byte> headerBytes = bytes.Slice(0, FrameHeader.Size);
            FrameHeader.Write(headerBytes.Span, bytes.Length, serializationContext.MessageSize);

            return _pipeStream.WriteAsync(bytes, token);
        }

        public ValueTask SendFrame(Message message, CancellationToken token = default)
        {
            var msgSize = message.CalculateSize();

            using var memoryOwner = MemoryPool<byte>.Shared.Rent(FrameHeader.Size + msgSize);
            Memory<byte> frameBytes = memoryOwner.Memory.Slice(0, FrameHeader.Size + msgSize);
            //#1 : frame header
            Memory<byte> headerBytes = frameBytes.Slice(0, FrameHeader.Size);
            FrameHeader.Write(headerBytes.Span, frameBytes.Length, msgSize);
            //#2 : Message
            Memory<byte> messageBytes = frameBytes.Slice(FrameHeader.Size);
            message.WriteTo(messageBytes.Span);

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

            public FrameHeader(int totalSize, int messageSize)
            {
                TotalSize = totalSize;
                MessageSize = messageSize;
            }

            public int TotalSize { get; }

            public int MessageSize { get; }

            public int PayloadSize => TotalSize - MessageSize;

            public static FrameHeader FromSpan(ReadOnlySpan<byte> span)
            {
                return MemoryMarshal.Read<FrameHeader>(span);
            }

            public static void Write(Span<byte> destination, int totalSize, int frameSize)
            {
                long bytes = totalSize + ((long)frameSize << 32);
                MemoryMarshal.Write(destination, ref bytes);
            }

            #region Equality 

            public override int GetHashCode()
            {
                unchecked
                {
                    return (TotalSize * 397) ^ MessageSize;
                }
            }

            public bool Equals(FrameHeader other) => TotalSize == other.TotalSize && MessageSize == other.MessageSize;

            public override bool Equals(object? obj) => obj is FrameHeader other && Equals(other);

            public static bool operator ==(FrameHeader left, FrameHeader right) => left.Equals(right);

            public static bool operator !=(FrameHeader left, FrameHeader right) => !left.Equals(right);

            #endregion

            public override string ToString() => $"[{nameof(TotalSize)} = {TotalSize}],[{nameof(MessageSize)} ={MessageSize}],[{nameof(PayloadSize)} ={PayloadSize}]";
        }
    }
}