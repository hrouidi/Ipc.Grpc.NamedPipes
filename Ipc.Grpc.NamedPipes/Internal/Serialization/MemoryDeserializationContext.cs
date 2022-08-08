using System;
using System.Buffers;
using Grpc.Core;

namespace Ipc.Grpc.NamedPipes.Internal
{

    public class MemoryDeserializationContext : DeserializationContext
    {
        private readonly Memory<byte> _bytes;

        public MemoryDeserializationContext(Memory<byte> bytes)
        {
            _bytes = bytes;
            PayloadLength = _bytes.Length;
        }

        public Span<byte> GetSpan()
        {
            return _bytes.Span;
        }

        public override int PayloadLength { get; }

        public override byte[] PayloadAsNewBuffer()
        {
            return _bytes.ToArray();
        }

        public override ReadOnlySequence<byte> PayloadAsReadOnlySequence()
        {
            return new ReadOnlySequence<byte>(_bytes);
        }
    }
}
