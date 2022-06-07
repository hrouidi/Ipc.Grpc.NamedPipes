#nullable enable
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Ipc.Grpc.NamedPipes.Internal.Helpers;

using Ipc.Grpc.NamedPipes.TransportProtocol;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class NamedPipeTransportV2 : IDisposable
    {
        private readonly byte[] _frameHeaderBytes;//= new byte[FrameHeader.Size];
        private readonly PipeStream _pipeStream;

        public NamedPipeTransportV2(PipeStream pipeStream)
        {
            _pipeStream = pipeStream;
            _frameHeaderBytes = ArrayPool<byte>.Shared.Rent(FrameHeader.Size);
        }


        //TODO : make this allocation free
        public async ValueTask<(Frame, Memory<byte>? payloadBytes)> ReadFrame(CancellationToken token = default)
        {
            int readBytes = await _pipeStream.ReadAsync(_frameHeaderBytes, 0, FrameHeader.Size, token)
                                             .ConfigureAwait(false);
            Debug.Assert(readBytes == FrameHeader.Size, "Client does not speak my dialect :/");

            FrameHeader header = FrameHeader.FromSpan(_frameHeaderBytes.AsSpan().Slice(0, FrameHeader.Size));

            IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(header.TotalSize);
            Memory<byte> buffer = owner.Memory.Slice(0, header.TotalSize);

            readBytes = await _pipeStream.ReadAsync(buffer, token)
                                         .ConfigureAwait(false);
            Debug.Assert(readBytes == header.TotalSize, "Client is a layer !");
            Frame? message = Frame.Parser.ParseFrom(buffer.Span.Slice(0, header.FrameSize));
            if (header.PayloadSize == 0)
            {
                owner.Dispose();
                return (message, null);
            }
            var payloadBytes = buffer.Slice(header.FrameSize);
            return (message, payloadBytes);
        }

        //TODO: Optimize memory allocation here
        public async ValueTask SendFrame(Frame message, Action<MemoryStream>? payloadSerializer, CancellationToken token = default)
        {
            using MemoryStream ms = new();
            message.WriteTo(ms);
            int frameSize = (int)ms.Length;
            payloadSerializer?.Invoke(ms);
            using IMemoryOwner<byte>? memoryOwner = MemoryPool<byte>.Shared.Rent(FrameHeader.Size);
            Memory<byte> bytes = memoryOwner.Memory.Slice(0, FrameHeader.Size);
            var header = new FrameHeader((int)ms.Length, frameSize);
            FrameHeader.ToSpan(bytes.Span, ref header);
            await _pipeStream.WriteAsync(bytes, token).ConfigureAwait(false);
            ms.WriteTo(_pipeStream);
        }
        public async ValueTask SendFrame2(Frame message, Func<Frame, (Memory<byte>, int)> messageSerializer, CancellationToken token = default)
        {
            //Serialize Frame message & payload if any
            (Memory<byte> messageBytes, int payloadSize) = messageSerializer.Invoke(message);

            //Header to bytes
            var header = new FrameHeader(messageBytes.Length, messageBytes.Length - payloadSize);
            using IMemoryOwner<byte>? memoryOwner = MemoryPool<byte>.Shared.Rent(FrameHeader.Size);
            Memory<byte> headerBytes = memoryOwner.Memory.Slice(0, FrameHeader.Size);
            FrameHeader.ToSpan(headerBytes.Span, ref header);

            //#1 : Write header bytes (always fixed size = 8 bytes) [total size of Frame + payload,size of Frame ]
            await _pipeStream.WriteAsync(headerBytes, token).ConfigureAwait(false);
            //#2 :  Write Frame message + payload if any
            await _pipeStream.WriteAsync(messageBytes, token).ConfigureAwait(false);
            //#3 : release messageBytes memory
            //TODO:
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_frameHeaderBytes);
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = Size)]
    public readonly struct FrameHeader : IEquatable<FrameHeader>
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

        //TODO: Optimize ToSpan 
        public static void ToSpan(Span<byte> destination, ref FrameHeader frameHeader)
        {
            MemoryMarshal.Write(destination, ref frameHeader);
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
}