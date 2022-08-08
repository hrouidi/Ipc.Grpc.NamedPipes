using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using Grpc.Core;

namespace Ipc.Grpc.NamedPipes.Internal.Transport;

public sealed partial class Message : IDisposable
{

    public static Message Eof = new();

    private readonly IMemoryOwner<byte> _memoryOwner;
    private readonly Memory<byte> _payloadBytes;

    private readonly MemoryMappedFile _mmf;
    private readonly int _offset;
    private readonly int _size;

    public Message(Memory<byte> payloadBytes, IMemoryOwner<byte> memoryOwner)
    {
        _memoryOwner = memoryOwner;
        _payloadBytes = payloadBytes;
    }

    public Message(MemoryMappedFile mmf, int offset, int size)
    {
        _mmf = mmf;
        _offset = offset;
        _size = size;
    }

    public TPayload GetPayload<TPayload>(Func<DeserializationContext, TPayload> deserializer)
    {
        DeserializationContext deserializationContext = GetDeserializationContext();
        TPayload ret = deserializer(deserializationContext);
        return ret;

        DeserializationContext GetDeserializationContext()
        {
            if (_mmf != null)
                return new SharedMemoryDeserializationContext(_mmf, _offset, _size);
            return new MemoryDeserializationContext(_payloadBytes);
        }
    }

    public void Dispose() => _memoryOwner?.Dispose();

}

public readonly struct MessageInfo<TPayload> : IEquatable<MessageInfo<TPayload>> where TPayload : class
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

    #region Equality semantic

    public bool Equals(MessageInfo<TPayload> other)
    {
        return Message.Equals(other.Message) &&
               EqualityComparer<TPayload>.Default.Equals(Payload, other.Payload) &&
               PayloadSerializer.Equals(other.PayloadSerializer);
    }

    public override bool Equals(object? obj)
    {
        return obj is MessageInfo<TPayload> other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = Message.GetHashCode();
            hashCode = (hashCode * 397) ^ EqualityComparer<TPayload>.Default.GetHashCode(Payload);
            hashCode = (hashCode * 397) ^ PayloadSerializer.GetHashCode();
            return hashCode;
        }
    }

    public static bool operator ==(MessageInfo<TPayload> left, MessageInfo<TPayload> right) => left.Equals(right);

    public static bool operator !=(MessageInfo<TPayload> left, MessageInfo<TPayload> right) => !left.Equals(right);

    #endregion
}