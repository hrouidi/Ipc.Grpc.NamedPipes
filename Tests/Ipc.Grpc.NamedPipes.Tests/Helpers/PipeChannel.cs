using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using PipeOptions = System.IO.Pipes.PipeOptions;

namespace Ipc.Grpc.NamedPipes.Tests.Helpers;

public class PipeChannel : IDisposable
{
    public NamedPipeClientStream ClientStream { get; set; }
    public NamedPipeServerStream ServerStream { get; set; }

    public static PipeChannel Create(string pipeName)
    {

        PipeChannel ret = new()
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
    public static PipeChannel CreateRandom()
    {
        return Create(Guid.NewGuid().ToString());
    }

    public void Dispose()
    {
        ClientStream.Dispose();
        ServerStream.Dispose();
    }
}