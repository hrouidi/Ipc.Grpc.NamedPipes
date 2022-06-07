using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using PipeOptions = System.IO.Pipes.PipeOptions;

namespace Ipc.Grpc.NamedPipes.Benchmarks;

public class PipeStreamCouple
{
    public NamedPipeClientStream ClientStream { get; set; }
    public NamedPipeServerStream ServerStream { get; set; }

    public static PipeStreamCouple Create(string pipeName)
    {

        PipeStreamCouple ret = new()
        {
            ClientStream = new NamedPipeClientStream(".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous,
                System.Security.Principal.TokenImpersonationLevel.None,
                HandleInheritability.None),

            ServerStream = new NamedPipeServerStream(pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous, 0, 0)
        };

        Task.WaitAll(ret.ClientStream.ConnectAsync(), ret.ServerStream.WaitForConnectionAsync());
        ret.ClientStream.ReadMode = PipeTransmissionMode.Message;
        return ret;

    }

    public void Dispose()
    {
        ClientStream.Dispose();
        ServerStream.Dispose();
    }
}