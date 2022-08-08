using System.Buffers;
using System.IO.MemoryMappedFiles;
using Grpc.Core;

namespace Ipc.Grpc.NamedPipes.Internal
{

    internal class SharedMemoryDeserializationContext : DeserializationContext
    {
        private readonly MemoryMappedFile _mmf;
        private readonly int _offset;

        public SharedMemoryDeserializationContext(MemoryMappedFile mmf, int offset, int size)
        {
            _mmf = mmf;
            _offset = offset;
            PayloadLength = size;
        }

        public override int PayloadLength { get; }

        public override byte[] PayloadAsNewBuffer()
        {
            using MemoryMappedViewAccessor accessor = _mmf.CreateViewAccessor();
            return accessor.GetSpan().Slice(_offset, PayloadLength).ToArray();
        }

        public override ReadOnlySequence<byte> PayloadAsReadOnlySequence()
        {
            using MemoryMappedViewAccessor accessor = _mmf.CreateViewAccessor();
            return new ReadOnlySequence<byte>(accessor.GetSpan().Slice(_offset, PayloadLength).ToArray());
        }
    }
}
