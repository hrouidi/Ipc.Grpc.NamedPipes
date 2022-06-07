
using System.IO;
using System.Threading.Tasks;
using Ipc.Grpc.NamedPipes.Protocol;

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
        ValueTask HandleRequestStreamPayload(byte[] payload);
        ValueTask HandleRequestStreamEnd();
        void HandleCancel();
    }
}