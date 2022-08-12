using System;
using System.Buffers;
using System.Diagnostics;
using Grpc.Core;
using Google.Protobuf;
using Ipc.Grpc.NamedPipes.Internal.Transport;
using Message = Ipc.Grpc.NamedPipes.Internal.Transport.Message;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class MemorySerializationContext : SerializationContext, IDisposable
    {
        private readonly Message _message;

        private IMemoryOwner<byte> _memoryOwner;
        private Memory<byte> _payloadBytes;
        private MemoryBufferWriter _writer;
        private int _messageSize;
        private int _payloadSize;
       
        public Memory<byte> FrameBytes { get; private set; }

        public MemorySerializationContext(Message message)
        {
            _message = message;
        }

        public override void Complete(byte[] payload) // worst case , buffer is already allocated
        {
            _messageSize = _message.CalculateSize();
            _payloadSize = payload.Length;
            int padding = NamedPipeTransport.FrameHeader.Size;
            //All
            _memoryOwner = MemoryPool<byte>.Shared.Rent(padding + _messageSize + payload.Length);
            Memory<byte> bytes = _memoryOwner.Memory.Slice(0, padding + _messageSize + payload.Length);
            //#1 : Header (message meta date)
            Memory<byte> headerBytes = bytes.Slice(0, NamedPipeTransport.FrameHeader.Size);
            NamedPipeTransport.FrameHeader.Write(headerBytes.Span, _messageSize, _payloadSize);
            //#2 : Message
            Memory<byte> messageBytes = bytes.Slice(padding, _messageSize);
            _message.WriteTo(messageBytes.Span);
            //#3 : payload 
            _payloadBytes = bytes.Slice(padding + _messageSize);
            payload.AsMemory().CopyTo(_payloadBytes);//Damn
            FrameBytes = bytes;
        }

        public override IBufferWriter<byte> GetBufferWriter()
        {
            return _writer ??= new MemoryBufferWriter(_payloadBytes);
        }

        public override void SetPayloadLength(int payloadLength) // best case: size is known before allocating the buffer
        {
            _messageSize = _message.CalculateSize();
            _payloadSize = payloadLength;
            int padding = NamedPipeTransport.FrameHeader.Size;
            //All
            _memoryOwner = MemoryPool<byte>.Shared.Rent(padding + _messageSize + _payloadSize);
            FrameBytes = _memoryOwner.Memory.Slice(0, padding + _messageSize + _payloadSize);
            //#1 : Header (message meta date)
            Memory<byte> headerBytes = FrameBytes.Slice(0, NamedPipeTransport.FrameHeader.Size);
            NamedPipeTransport.FrameHeader.Write(headerBytes.Span, _messageSize, _payloadSize);
            //#2 : Message
            Memory<byte> messageBytes = FrameBytes.Slice(padding, _messageSize);
            _message.WriteTo(messageBytes.Span);
            //#3 : payload will be set in MemoryBufferWriter
            _payloadBytes = FrameBytes.Slice(padding + _messageSize);
        }

        public override void Complete()
        {
            Debug.Assert(FrameBytes.Length == NamedPipeTransport.FrameHeader.Size + _messageSize + _payloadBytes.Length);
        }

        public void Dispose()
        {
            _memoryOwner?.Dispose();
        }

        internal sealed class MemoryBufferWriter : IBufferWriter<byte>
        {
            private int _position;

            public Memory<byte> Bytes { get; }

            public MemoryBufferWriter(Memory<byte> bytes)
            {
                Bytes = bytes;
            }

            public void Advance(int count)
            {
                _position += count;
            }

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                return Bytes.Slice(_position);
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                return Bytes.Slice(_position).Span;
            }
        }
    }
}
