
using System.IO;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.TransportProtocol;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal static class SerializationHelpers
    {
        public static byte[] Serialize<T>(Marshaller<T> marshaller, T message)
        {
            var serializationContext = new ByteArraySerializationContext();
            marshaller.ContextualSerializer(message, serializationContext);
            return serializationContext.SerializedData;
        }

        public static T Deserialize<T>(Marshaller<T> marshaller, byte[] payload)
        {
            var deserializationContext = new ByteArrayDeserializationContext(payload);
            return marshaller.ContextualDeserializer(deserializationContext);
        }

        public static void Serialize<T>(MemoryStream stream, Marshaller<T> marshaller, T message)
        {
            var serializationContext = new StreamSerializationContext(stream);
            marshaller.ContextualSerializer(message, serializationContext);
        }

        public static T Deserialize<T>(Marshaller<T> marshaller, MemoryStream stream)
        {
            var deserializationContext = new StreamDeserializationContext(stream);
            return marshaller.ContextualDeserializer(deserializationContext);
        }

        public static void Serialize<TPayload>(Frame frame, Marshaller<TPayload> marshaller, TPayload message)
        {
            var serializationContext = new MemorySerializationContext(frame);
            marshaller.ContextualSerializer(message, serializationContext);
        }
    }
}
