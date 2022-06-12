#nullable enable
using System;
using System.Buffers;
using System.Collections.Generic;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.TransportProtocol;

namespace Ipc.Grpc.NamedPipes.Internal;


//TODO: replace by Message class (partial class extended)
internal sealed class Frame : IDisposable//where TPayload : class 
{
    private readonly IMemoryOwner<byte> _memoryOwner;
    private readonly Memory<byte> _payloadBytes;

    public Message Message { get; }

    public TPayload GetPayload<TPayload>(Func<DeserializationContext, TPayload> deserializer)
    {
        var deserializationContext = new MemoryDeserializationContext(_payloadBytes);
        TPayload ret = deserializer(deserializationContext);
        return ret;
    }

    public Frame(Message message, Memory<byte> payloadBytes, IMemoryOwner<byte> memoryOwner)
    {
        Message = message;
        _memoryOwner = memoryOwner;
        _payloadBytes = payloadBytes;
    }

    public void Dispose() => _memoryOwner.Dispose();
}

// TODO: rename to MessageInfo
internal readonly struct FrameInfo<TPayload> : IEquatable<FrameInfo<TPayload>> where TPayload : class
{
    public Message Message { get; }

    public TPayload Payload { get; }

    public Action<TPayload, SerializationContext> PayloadSerializer { get; }

    public FrameInfo(Message message, TPayload payload, Action<TPayload, SerializationContext> payloadSerializer)
    {
        Message = message;
        Payload = payload;
        PayloadSerializer = payloadSerializer;
    }

    #region Equality semantic

    public bool Equals(FrameInfo<TPayload> other)
    {
        return Message.Equals(other.Message) &&
               EqualityComparer<TPayload>.Default.Equals(Payload, other.Payload) &&
               PayloadSerializer.Equals(other.PayloadSerializer);
    }

    public override bool Equals(object? obj)
    {
        return obj is FrameInfo<TPayload> other && Equals(other);
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

    public static bool operator ==(FrameInfo<TPayload> left, FrameInfo<TPayload> right) => left.Equals(right);

    public static bool operator !=(FrameInfo<TPayload> left, FrameInfo<TPayload> right) => !left.Equals(right);

    #endregion
}