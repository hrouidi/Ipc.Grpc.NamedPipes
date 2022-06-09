#nullable enable
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal.Helpers;

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

        public async ValueTask<Packet> ReadFrame(CancellationToken token = default)
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

            Frame? message = Frame.Parser.ParseFrom(framePlusPayloadBytes.Span.Slice(0, header.FrameSize));
            //if (header.PayloadSize == 0)
            //{
            //    owner.Dispose();
            //    return (message, null, null);
            //}
            var payloadBytes = framePlusPayloadBytes.Slice(header.FrameSize);
            var packet = new Packet(message, payloadBytes, owner);
            return packet;
        }

        public async ValueTask SendFrame<TPayload>(PacketInfo<TPayload> packet, CancellationToken token = default)
        {
            using MemorySerializationContext serializationContext = new(packet.Frame);
            packet.Serializer(packet.Payload, serializationContext);
            
            Memory<byte> bytes = serializationContext.Bytes;

            Memory<byte> headerBytes = bytes.Slice(0, FrameHeader.Size);
            FrameHeader.Write(headerBytes.Span, bytes.Length, serializationContext.FrameSize);

            await _pipeStream.WriteAsync(bytes, token).ConfigureAwait(false);
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_frameHeaderBytes);
        }

        [StructLayout(LayoutKind.Sequential, Size = Size)]
        private readonly struct FrameHeader : IEquatable<FrameHeader>
        {
            public const int Size = 2 * sizeof(int);
            public FrameHeader(int totalSize, int frameSize)
            {
                TotalSize = totalSize;
                FrameSize = frameSize;
            }

            public int TotalSize { get; }

            public int FrameSize { get; }

            public int PayloadSize => TotalSize - FrameSize;

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
                    return (TotalSize * 397) ^ FrameSize;
                }
            }

            public bool Equals(FrameHeader other) => TotalSize == other.TotalSize && FrameSize == other.FrameSize;

            public override bool Equals(object? obj) => obj is FrameHeader other && Equals(other);

            public static bool operator ==(FrameHeader left, FrameHeader right) => left.Equals(right);

            public static bool operator !=(FrameHeader left, FrameHeader right) => !left.Equals(right);

            #endregion

            public override string ToString() => $"[{nameof(TotalSize)} = {TotalSize}],[{nameof(FrameSize)} ={FrameSize}],[{nameof(PayloadSize)} ={PayloadSize}]";
        }

        public readonly struct PacketInfo<TPayload> : IEquatable<PacketInfo<TPayload>>//where TPayload : class 
        {
            public Action<TPayload, SerializationContext> Serializer { get; }

            public Frame Frame { get; }

            public TPayload Payload { get; }

            public PacketInfo(Frame frame, TPayload payload, Action<TPayload, SerializationContext> payloadContextualSerializer)
            {
                Frame = frame;
                Payload = payload;
                Serializer = payloadContextualSerializer;
            }

            #region Equality semantic
            public bool Equals(PacketInfo<TPayload> other)
            {
                return Serializer.Equals(other.Serializer) && 
                       Frame.Equals(other.Frame) && 
                       EqualityComparer<TPayload>.Default.Equals(Payload, other.Payload);
            }

            public override bool Equals(object? obj) => obj is PacketInfo<TPayload> other && Equals(other);

            public override int GetHashCode() => (Serializer, Frame, Payload).GetHashCode();

            public static bool operator ==(PacketInfo<TPayload> left, PacketInfo<TPayload> right) => left.Equals(right);

            public static bool operator !=(PacketInfo<TPayload> left, PacketInfo<TPayload> right) => !left.Equals(right);

            #endregion
        }

        public sealed class Packet : IDisposable//where TPayload : class 
        {
            private readonly IMemoryOwner<byte> _memoryOwner;
            private readonly Memory<byte> _payloadBytes;

            public Frame Frame { get; }

            public TPayload GetPayload<TPayload>(Func<DeserializationContext, TPayload> deserializer)
            {
                var deserializationContext = new MemoryDeserializationContext(_payloadBytes);
                TPayload ret = deserializer(deserializationContext);
                return ret;
            }

            public Packet(Frame frame, Memory<byte> payloadBytes, IMemoryOwner<byte> memoryOwner)
            {
                Frame = frame;
                _memoryOwner = memoryOwner;
                _payloadBytes = payloadBytes;

            }

            public void Dispose()
            {
                _memoryOwner?.Dispose();
            }
        }
    }
}