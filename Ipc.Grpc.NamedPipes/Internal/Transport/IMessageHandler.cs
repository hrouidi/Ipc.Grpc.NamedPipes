
using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using Ipc.Grpc.NamedPipes.TransportProtocol;
using Headers = Ipc.Grpc.NamedPipes.Protocol.Headers;
using Request = Ipc.Grpc.NamedPipes.Protocol.Request;
using Response = Ipc.Grpc.NamedPipes.Protocol.Response;

namespace Ipc.Grpc.NamedPipes.Internal
{
    public interface IServerMessageHandler
    {
        void HandleResponseHeaders(Headers headers);
        ValueTask HandleResponseStreamPayload(byte[] payload);
        ValueTask HandleResponseAsync(Response response, byte[] payload = null);
    }

    public interface IClientMessageHandler
    {
        void HandleRequest(Request message, byte[] req);
        ValueTask HandleUnaryRequest(Request message,MemoryStream requestPayload);
        ValueTask HandleUnaryRequest2(TransportProtocol.Request request,Memory<byte> requestPayload, IMemoryOwner<byte> owner);
        ValueTask HandleRequestStreamPayload(byte[] payload);
        ValueTask HandleRequestStreamEnd();
        void HandleCancel();
    }
}