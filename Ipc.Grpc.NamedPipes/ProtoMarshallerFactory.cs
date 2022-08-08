using Google.Protobuf;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal;

namespace Ipc.Grpc.NamedPipes;

public class ProtoMarshallerFactory<TMessage> where TMessage : IMessage<TMessage>, IBufferMessage, new()
{
    private static readonly MessageParser<TMessage> _parser = new(() => new TMessage());
    public static Marshaller<TMessage> Create() => Marshallers.Create(SerializeMessage, DeserializeMessage);

    private static void SerializeMessage(TMessage message, SerializationContext context)
    {
        if (context is MemorySerializationContext optimizedContext)
        {
            optimizedContext.SetPayloadLength(message.CalculateSize());
            message.WriteTo(optimizedContext.GetSpan());
        }
        else
        {
            context.SetPayloadLength(message.CalculateSize());
            message.WriteTo(context.GetBufferWriter());
            context.Complete();
        }
    }
    private static TMessage DeserializeMessage(DeserializationContext context)
    {
        if (context is MemoryDeserializationContext optimizedContext)
            return _parser.ParseFrom(optimizedContext.GetSpan());

        return _parser.ParseFrom(context.PayloadAsReadOnlySequence());
    }
}