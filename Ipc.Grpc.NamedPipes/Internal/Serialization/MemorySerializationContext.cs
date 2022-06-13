using System;
using System.Buffers;
using System.Diagnostics;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.TransportProtocol;
using Google.Protobuf;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class MemorySerializationContext : SerializationContext, IDisposable
    {
        private readonly Message _message;

        private Memory<byte> _payloadBytes;

        private MemoryBufferWriter _writer;

        public int MessageSize { get; }

        public int PayloadSize { get; private set; }

        public Memory<byte> Bytes { get; private set; }

        public IMemoryOwner<byte> MemoryOwner { get; private set; }

        public MemorySerializationContext(Message message)
        {
            _message = message;
            MessageSize = _message.CalculateSize();
        }

        public override void Complete(byte[] payload) // worst case , buffer is already allocated
        {
            PayloadSize = payload.Length;
            int padding = Transport.FrameHeader.Size;
            //All
            MemoryOwner = MemoryPool<byte>.Shared.Rent(padding + MessageSize + payload.Length);
            Memory<byte> bytes = MemoryOwner.Memory.Slice(0, padding + MessageSize + payload.Length);
            //#1 : will be set later in send method

            //#2 : Message
            Memory<byte> messageBytes = bytes.Slice(padding, MessageSize);
            _message.WriteTo(messageBytes.Span);
            //#3 : payload 
            _payloadBytes = bytes.Slice(padding + MessageSize);
            payload.AsMemory().CopyTo(_payloadBytes);//Damn
            Bytes = bytes;
        }

        public override IBufferWriter<byte> GetBufferWriter()
        {
            return _writer ??= new MemoryBufferWriter(_payloadBytes);
        }

        public override void SetPayloadLength(int payloadLength) // best case: size is known before allocating the buffer
        {
            PayloadSize = payloadLength;
            int padding = Transport.FrameHeader.Size;
            //All
            MemoryOwner = MemoryPool<byte>.Shared.Rent(padding + MessageSize + PayloadSize);
            Bytes = MemoryOwner.Memory.Slice(0, padding + MessageSize + PayloadSize);
            //#1 : will be set later in send method

            //#2 : Message
            Memory<byte> messageBytes = Bytes.Slice(padding, MessageSize);
            _message.WriteTo(messageBytes.Span);
            //#3 : payload will be set in MemoryBufferWriter
            _payloadBytes = Bytes.Slice(padding + MessageSize);
        }

        public override void Complete()
        {
            Debug.Assert(Bytes.Length == Transport.FrameHeader.Size + MessageSize + _payloadBytes.Length);
        }

        public void Dispose()
        {
            MemoryOwner?.Dispose();
        }
    }
}
