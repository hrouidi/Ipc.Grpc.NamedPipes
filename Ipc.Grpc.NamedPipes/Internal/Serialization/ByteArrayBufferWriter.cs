using System;
using System.Buffers;
using System.IO;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class ByteArrayBufferWriter : IBufferWriter<byte>
    {
        private readonly byte[] _buffer;
        private int _position;

        public ByteArrayBufferWriter(int size)
        {
            _buffer = new byte[size];
        }

        public void Advance(int count)
        {
            _position += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0) => _buffer.AsMemory(_position);

        public Span<byte> GetSpan(int sizeHint = 0) => _buffer.AsSpan(_position);

        public byte[] Buffer => _buffer;
    }

    internal class MemoryStreamBufferWriter : IBufferWriter<byte>
    {
        private int _position;

        public MemoryStream Stream { get; }

        public MemoryStreamBufferWriter(MemoryStream stream, int newCapacity)
        {
            Stream = stream;
            _position = (int)stream.Position;
            Stream.SetLength(stream.Length + newCapacity);
        }

        public void Advance(int count)
        {
            _position += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            return Stream.GetBuffer().AsMemory(_position);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            return Stream.GetBuffer().AsSpan(_position);
        }
    }
}
