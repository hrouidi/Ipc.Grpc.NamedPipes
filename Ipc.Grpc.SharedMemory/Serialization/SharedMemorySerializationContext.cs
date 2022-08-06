using System.Buffers;
using System.Diagnostics;
using Google.Protobuf;
using Grpc.Core;
using Message = Ipc.Grpc.NamedPipes.Internal.Transport.Message;

namespace Ipc.Grpc.SharedMemory.Serialization
{
    internal class SharedMemorySerializationContext : SerializationContext, IDisposable
    {
        internal class SharedMemoryBufferWriter : IBufferWriter<byte>
        {
            private int _position;

            public Memory<byte> Stream { get; }

            public SharedMemoryBufferWriter(Memory<byte> stream)
            {
                Stream = stream;
            }

            public void Advance(int count)
            {
                _position += count;
            }

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                return Stream.Slice(_position);
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                return Stream.Slice(_position).Span;
            }
        }

        private readonly Message _message;
        private readonly int _messageSize;

        private int _payloadSize;
        private SharedMemoryBufferWriter _writer;
        private Memory<byte> _payloadBytes;

        public Memory<byte> Bytes { get; private set; }

        public IMemoryOwner<byte> MemoryOwner { get; private set; }

        public SharedMemorySerializationContext(Message message)
        {
            _message = message;
            _messageSize = _message.CalculateSize();
        }

        public override void Complete(byte[] payload) // worst case , buffer is already allocated
        {
            _payloadSize = payload.Length;
            int padding = SharedMemoryTransport.FrameHeader.Size;
            //All
            MemoryOwner = MemoryPool<byte>.Shared.Rent(padding + _messageSize + payload.Length);
            Memory<byte> bytes = MemoryOwner.Memory.Slice(0, padding + _messageSize + payload.Length);
            //#1 : will be set later in send method

            //#2 : Message
            Memory<byte> messageBytes = bytes.Slice(padding, _messageSize);
            _message.WriteTo(messageBytes.Span);
            //#3 : payload 
            _payloadBytes = bytes.Slice(padding + _messageSize);
            payload.AsMemory().CopyTo(_payloadBytes);//Damn
            Bytes = bytes;
        }

        public override IBufferWriter<byte> GetBufferWriter()
        {
            return _writer ??= new SharedMemoryBufferWriter(_payloadBytes);
        }

        public override void SetPayloadLength(int payloadLength) // best case: size is known before allocating the buffer
        {
            _payloadSize = payloadLength;
            int padding = SharedMemoryTransport.FrameHeader.Size;
            //All
            MemoryOwner = MemoryPool<byte>.Shared.Rent(padding + _messageSize + _payloadSize);
            Bytes = MemoryOwner.Memory.Slice(0, padding + _messageSize + _payloadSize);
            //#1 : will be set later in send method

            //#2 : Message
            Memory<byte> messageBytes = Bytes.Slice(padding, _messageSize);
            _message.WriteTo(messageBytes.Span);
            //#3 : payload will be set in MemoryBufferWriter
            _payloadBytes = Bytes.Slice(padding + _messageSize);
        }

        public override void Complete()
        {
            Memory<byte> frameBytes = Bytes;
            Memory<byte> headerBytes = frameBytes.Slice(0, SharedMemoryTransport.FrameHeader.Size);
            SharedMemoryTransport.FrameHeader.Write(headerBytes.Span, _messageSize, _payloadSize);

            Debug.Assert(Bytes.Length == SharedMemoryTransport.FrameHeader.Size + _messageSize + _payloadBytes.Length);
        }

        public void Dispose()
        {
            MemoryOwner?.Dispose();
        }
    }
}
