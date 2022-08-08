using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using Grpc.Core;
using Google.Protobuf;
using Ipc.Grpc.NamedPipes.Internal.Transport;
using Message = Ipc.Grpc.NamedPipes.Internal.Transport.Message;

namespace Ipc.Grpc.NamedPipes.Internal
{
    public class SharedMemorySerializationContext : SerializationContext, IDisposable
    {
        internal class MemoryBufferWriter : IBufferWriter<byte>
        {
            private int _position;

            public Memory<byte> Stream { get; }

            public MemoryBufferWriter(Memory<byte> stream)
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
        private MemoryBufferWriter _writer;

        public int GlobalSize { get; private set; }
        public Guid MapName { get; private set; }
        public MemoryMappedFile Mmf { get; private set; }

        public SharedMemorySerializationContext(Message message)
        {
            _message = message;
            _messageSize = _message.CalculateSize();
        }

        public override void Complete(byte[] payload) // worst case , buffer is already allocated
        {
            _payloadSize = payload.Length;
            int padding = NamedPipeTransport.FrameHeader.Size;
            GlobalSize = NamedPipeTransport.FrameHeader.Size + _messageSize + payload.Length;
            //All
            MapName = Guid.NewGuid();
            Mmf = MemoryMappedFile.CreateNew(MapName.ToString(), GlobalSize);
            using MemoryMappedViewAccessor accessor = Mmf.CreateViewAccessor(0, GlobalSize);
            Span<byte> bytes = accessor.GetSpan();

            //#1 : header
            Span<byte> headerBytes = bytes.Slice(0, padding);
            NamedPipeTransport2.FrameHeader.Write(headerBytes, _messageSize, _payloadSize);
            //#2 : Message
            Span<byte> messageBytes = bytes.Slice(padding, _messageSize);
            _message.WriteTo(messageBytes);
            //#3 : payload 
            Span<byte> payloadBytes = bytes.Slice(padding + _messageSize);
            payload.CopyTo(payloadBytes);//Damn
        }

        private static MemoryMappedFile Write(string mapName, IMessage message, int globalSize, int messageSize, int payloadSize)
        {
            var ret = MemoryMappedFile.CreateNew(mapName, globalSize);
            using MemoryMappedViewAccessor accessor = ret.CreateViewAccessor(0, globalSize);
            Span<byte> bytes = accessor.GetSpan();
            //#1 : header
            int padding = NamedPipeTransport.FrameHeader.Size;
            Span<byte> headerBytes = bytes.Slice(0, padding);
            NamedPipeTransport2.FrameHeader.Write(headerBytes, messageSize, payloadSize);
            //#2 : Message
            Span<byte> messageBytes = bytes.Slice(padding, messageSize);
            message.WriteTo(messageBytes);
            return ret;
        }

        public override IBufferWriter<byte> GetBufferWriter()
        {
            return _writer;
        }

        public override void SetPayloadLength(int payloadLength) // best case: size is known before allocating the buffer
        {
            //PayloadSize = payloadLength;
            //int padding = NamedPipeTransport.FrameHeader.Size;
            ////All
            //MemoryOwner = MemoryPool<byte>.Shared.Rent(padding + MessageSize + PayloadSize);
            //Bytes = MemoryOwner.Memory.Slice(0, padding + MessageSize + PayloadSize);
            ////#1 : will be set later in send method
            //Memory<byte> headerBytes = Bytes.Slice(0, NamedPipeTransport2.FrameHeader.Size);
            //NamedPipeTransport2.FrameHeader.Write(headerBytes.Span, MessageSize, PayloadSize);
            ////#2 : Message
            //Memory<byte> messageBytes = Bytes.Slice(padding, MessageSize);
            //_message.WriteTo(messageBytes.Span);
            ////#3 : payload will be set in MemoryBufferWriter
            //_payloadBytes = Bytes.Slice(padding + MessageSize);
        }

        public override void Complete()
        {
            //Debug.Assert(Accessor.GetSpan().Length == NamedPipeTransport.FrameHeader.Size + MessageSize + _payloadBytes.Length);
        }

        public void Dispose()
        {
            Mmf.Dispose();
        }
    }
}
