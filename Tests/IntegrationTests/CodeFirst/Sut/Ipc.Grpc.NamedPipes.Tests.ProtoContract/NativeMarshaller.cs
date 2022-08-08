using Google.Protobuf;
using Grpc.Core;
using ProtoBuf.Grpc.Configuration;

namespace Ipc.Grpc.NamedPipes.Tests.ProtoContract;

public class NativeMarshaller
{
    public static void Setup()
    {
        Marshaller<ProtoMessage> request2Marshaller = ProtoMarshallerFactory<ProtoMessage>.Create();
        BinderConfiguration.Default.SetMarshaller(request2Marshaller);
    }
}

//public class MarshallerFactory<TMessage> where TMessage : IMessage<TMessage>, IBufferMessage, new()
//{
//    private static readonly MessageParser<TMessage> _parser = new (() => new TMessage());
//    public static Marshaller<TMessage> Create() 
//    {
        
//        return Marshallers.Create(SerializeMessage, DeserializeMessage);

//        static void SerializeMessage(TMessage message, SerializationContext context)
//        {
//            context.SetPayloadLength(message.CalculateSize());
//            message.WriteTo(context.GetBufferWriter());
//            //context.Complete();
//        }

        
//    }
//    private static TMessage DeserializeMessage(DeserializationContext context) 
//    {
//        return _parser.ParseFrom(context.PayloadAsReadOnlySequence());
//    }
//}