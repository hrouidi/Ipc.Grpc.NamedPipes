using System.ServiceModel;
using System.Threading.Tasks;
using ProtoBuf;
using ProtoBuf.Grpc;

namespace Ipc.Grpc.NamedPipes.Tests.ProtoContract;


[ProtoContract]
public sealed class RequestMessage
{
    [ProtoMember(1)]
    public int Value { get; set; }
    [ProtoMember(2)]
    public byte[] Binary { get; set; }
}

[ProtoContract]
public sealed class ResponseMessage
{
    [ProtoMember(1)]
    public int Value { get; set; }
    [ProtoMember(2)]
    public byte[] Binary { get; set; }
}

[ServiceContract]
public interface ITestService
{
    ValueTask<ResponseMessage> SimpleUnaryAsync(RequestMessage request, CallContext context = default);
}