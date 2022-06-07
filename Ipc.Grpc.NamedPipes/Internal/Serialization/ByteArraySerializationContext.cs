using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using Grpc.Core;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class ByteArraySerializationContext : SerializationContext
    {
        private int _payloadLength;
        private ByteArrayBufferWriter _bufferWriter;

        public override void Complete(byte[] payload)
        {
            SerializedData = payload;
        }

        public override IBufferWriter<byte> GetBufferWriter()
        {
            return _bufferWriter ??= new ByteArrayBufferWriter(_payloadLength);
        }

        public override void SetPayloadLength(int payloadLength)
        {
            _payloadLength = payloadLength;
        }

        public override void Complete()
        {
            Debug.Assert(_bufferWriter.Buffer.Length == _payloadLength);
            SerializedData = _bufferWriter.Buffer;
        }

        public byte[] SerializedData { get; private set; }

    }

    internal class StreamSerializationContext : SerializationContext
    {
        private int _payloadLength;
        private MemoryStreamBufferWriter _writer;
        private byte[] _serializedData;

        public MemoryStream Stream;


        public StreamSerializationContext(MemoryStream stream)
        {
            Stream = stream;
        }

        public override void Complete(byte[] payload)
        {
            _serializedData = payload;
        }

        public override IBufferWriter<byte> GetBufferWriter()
        {
            return _writer ??= new MemoryStreamBufferWriter(Stream, _payloadLength);
        }

        public override void SetPayloadLength(int payloadLength)
        {
            _payloadLength = payloadLength;
        }

        public override void Complete()
        {
            //await _writer.FlushAsync();
            //Debug.Assert(_stream.Length == _payloadLength);
        }
    }
}
