using Google.Protobuf;
using Grpc.Core;
using ProtoBuf.Grpc.Configuration;

namespace Ipc.Grpc.NamedPipes.Tests.ProtoContract;

public class NativeMarshaller
{
    public static void Setup()
    {
        Marshaller<ProtoMessage> request2Marshaller = Create(ProtoMessage.Parser);
        BinderConfiguration.Default.SetMarshaller(request2Marshaller);
    }
    

    private static Marshaller<TMessage> Create<TMessage>(MessageParser<TMessage> parser) where TMessage : IMessage<TMessage>, IBufferMessage
    {
        return Marshallers.Create(SerializeMessage, x => DeserializeMessage(x, parser));

        static void SerializeMessage(TMessage message, SerializationContext context)
        {
            context.SetPayloadLength(message.CalculateSize());
            message.WriteTo(context.GetBufferWriter());
            context.Complete();
        }

        static T DeserializeMessage<T>(DeserializationContext context, MessageParser<T> parser) where T : IMessage<T>
        {
            return parser.ParseFrom(context.PayloadAsReadOnlySequence());
        }
    }
}