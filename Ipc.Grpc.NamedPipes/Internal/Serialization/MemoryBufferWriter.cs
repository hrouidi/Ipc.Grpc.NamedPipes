using System;
using System.Buffers;
using System.IO;

namespace Ipc.Grpc.NamedPipes.Internal
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
}
