using System;
using System.Buffers;
using System.IO;
using System.Linq;
using Grpc.Core;

namespace Ipc.Grpc.NamedPipes.Internal
{

    internal class MemoryDeserializationContext : DeserializationContext
    {
        private readonly Memory<byte> _stream;

        public MemoryDeserializationContext(Memory<byte> stream)
        {
            _stream = stream;
        }

        public override int PayloadLength { get; }

        public override byte[] PayloadAsNewBuffer()
        {
            return _stream.ToArray();
        }

        public override ReadOnlySequence<byte> PayloadAsReadOnlySequence()
        {
            return new ReadOnlySequence<byte>(_stream);
        }
    }
}
