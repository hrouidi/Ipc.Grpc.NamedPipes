using System;
using System.Buffers;
using Grpc.Core;

namespace Ipc.Grpc.NamedPipes.Internal.Transport;

public sealed partial class Message : IDisposable
{
    private readonly IMemoryOwner<byte> _memoryOwner;
    private readonly Memory<byte> _payloadBytes;

    public Message(Memory<byte> payloadBytes, IMemoryOwner<byte> memoryOwner)
    {
        _memoryOwner = memoryOwner;
        _payloadBytes = payloadBytes;
    }

    public TPayload GetPayload<TPayload>(Func<DeserializationContext, TPayload> deserializer)
    {
        var deserializationContext = new MemoryDeserializationContext(_payloadBytes);
        TPayload ret = deserializer(deserializationContext);
        return ret;
    }

    public void Dispose() => _memoryOwner.Dispose();

}