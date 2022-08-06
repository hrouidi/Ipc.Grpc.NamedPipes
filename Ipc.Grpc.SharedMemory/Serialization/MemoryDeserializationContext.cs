using System.Buffers;
using Grpc.Core;

namespace Ipc.Grpc.SharedMemory.Serialization
{

    internal class MemoryDeserializationContext : DeserializationContext
    {
        private readonly Memory<byte> _bytes;

        public MemoryDeserializationContext(Memory<byte> bytes)
        {
            _bytes = bytes;
            PayloadLength = _bytes.Length;
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
