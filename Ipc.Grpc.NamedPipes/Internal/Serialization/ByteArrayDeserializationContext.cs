using System;
using System.Buffers;
using System.IO;
using System.Linq;
using Grpc.Core;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class ByteArrayDeserializationContext : DeserializationContext
    {
        private readonly byte[] _payload;

        public ByteArrayDeserializationContext(byte[] payload)
        {
            _payload = payload;
        }

        public override int PayloadLength => _payload.Length;

        public override byte[] PayloadAsNewBuffer() => _payload.ToArray();

        public override ReadOnlySequence<byte> PayloadAsReadOnlySequence() => new(_payload);
    }

    internal class StreamDeserializationContext : DeserializationContext
    {
        private readonly MemoryStream _stream;

        public StreamDeserializationContext(MemoryStream stream)
        {
            _stream = stream;
            PayloadLength = (int)(_stream.Length - _stream.Position);
        }

        public override int PayloadLength { get; }

        public override byte[] PayloadAsNewBuffer()
        {
            return _stream.ToArray()
                          .AsMemory()
                          .Slice((int)_stream.Position)
                          .ToArray();
        }

        public override ReadOnlySequence<byte> PayloadAsReadOnlySequence()
        {
            return new ReadOnlySequence<byte>(_stream.GetBuffer(), (int)_stream.Position, PayloadLength);
        }
    }
}
