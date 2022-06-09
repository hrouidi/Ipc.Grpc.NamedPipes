﻿using System;
using System.Buffers;
using System.Diagnostics;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.TransportProtocol;
using Google.Protobuf;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class MemorySerializationContext : SerializationContext, IDisposable
    {
        private readonly Frame _frame;

        private int _payloadLength;
        private Memory<byte> _payloadBytes;

        private MemoryBufferWriter _writer;

        public int FrameSize { get; }
        public Memory<byte> Bytes { get; private set; }

        public IMemoryOwner<byte> MemoryOwner { get; private set; }

        public MemorySerializationContext(Frame frame)
        {
            _frame = frame;
            FrameSize = _frame.CalculateSize();
        }

        public override void Complete(byte[] payload) // worst case , buffer is already allocated
        {
            int padding = NamedPipeTransportV2.FrameHeader.Size;
            //All
            MemoryOwner = MemoryPool<byte>.Shared.Rent(padding + FrameSize + payload.Length);
            Memory<byte> messageBytes = MemoryOwner.Memory.Slice(0, padding + FrameSize + _payloadLength);
            //#1 : will be set later in send method

            //#2 : frame
            Memory<byte> frameBytes = messageBytes.Slice(padding, FrameSize);
            _frame.WriteTo(frameBytes.Span);
            //#3 : payload 
            _payloadBytes = messageBytes.Slice(padding + FrameSize);
            payload.AsMemory().CopyTo(_payloadBytes);//Damn
            Bytes = messageBytes;

        }

        public override IBufferWriter<byte> GetBufferWriter()
        {
            return _writer ??= new MemoryBufferWriter(_payloadBytes);
        }

        public override void SetPayloadLength(int payloadLength) // best case: size is known before allocating the buffer
        {
            _payloadLength = payloadLength;
            int padding = NamedPipeTransportV2.FrameHeader.Size;
            //All
            MemoryOwner = MemoryPool<byte>.Shared.Rent(padding + FrameSize + _payloadLength);
            Bytes = MemoryOwner.Memory.Slice(0, padding + FrameSize + _payloadLength);
            //#1 : will be set later in send method

            //#2 : frame
            Memory<byte> frameBytes = Bytes.Slice(padding, FrameSize);
            _frame.WriteTo(frameBytes.Span);
            //#3 : payload will be set in MemoryBufferWriter
            _payloadBytes = Bytes.Slice(padding + FrameSize);
        }

        public override void Complete()
        {
            Debug.Assert(Bytes.Length == NamedPipeTransportV2.FrameHeader.Size + FrameSize + _payloadBytes.Length);
        }

        public void Dispose()
        {
            MemoryOwner.Dispose();
        }
    }
}