using System;
using System.Buffers;
using Grpc.Core;

namespace Ipc.Grpc.NamedPipes.Internal.Transport
{
    public sealed partial class Message : IDisposable
    {
        public static Message Eof = new();

        private readonly IMemoryOwner<byte> _memoryOwner;
        private readonly Memory<byte> _payloadBytes;

        public bool IsEof => ReferenceEquals(Eof, this);

        public Message(Memory<byte> payloadBytes, IMemoryOwner<byte> memoryOwner)
        {
            _memoryOwner = memoryOwner;
            _payloadBytes = payloadBytes;
        }

        public TPayload GetPayload<TPayload>(Func<DeserializationContext, TPayload> deserializer)
        {
            MemoryDeserializationContext deserializationContext = new(_payloadBytes);
            TPayload ret = deserializer(deserializationContext);
            return ret;
        }

        public void Dispose() => _memoryOwner?.Dispose();

    }

    internal readonly record struct MessageInfo<TPayload> where TPayload : class
    {
        public Message Message { get; }

        public TPayload Payload { get; }

        public Action<TPayload, SerializationContext> PayloadSerializer { get; }

        public MessageInfo(Message message, TPayload payload, Action<TPayload, SerializationContext> payloadSerializer)
        {
            Message = message;
            Payload = payload;
            PayloadSerializer = payloadSerializer;
        }
    }
}