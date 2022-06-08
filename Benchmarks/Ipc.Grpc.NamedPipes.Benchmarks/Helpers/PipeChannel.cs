using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using PipeOptions = System.IO.Pipes.PipeOptions;

namespace Ipc.Grpc.NamedPipes.Benchmarks.Helpers;

public sealed class PipeChannel : IDisposable
{
    public NamedPipeClientStream ClientStream { get; }
    public NamedPipeServerStream ServerStream { get; }

    private PipeChannel(NamedPipeClientStream clientStream, NamedPipeServerStream serverStream)
    {
        ClientStream = clientStream;
        ServerStream = serverStream;
    }

    public static PipeChannel Create(string pipeName)
    {
        PipeChannel ret = new(CreateClientPipe(pipeName), CreateServerPipe(pipeName));
        Task.WaitAll(ret.ClientStream.ConnectAsync(), ret.ServerStream.WaitForConnectionAsync());
        ret.ClientStream.ReadMode = PipeTransmissionMode.Message;
        return ret;
    }

    public static NamedPipeClientStream CreateClientPipe(string name)
    {
        return new NamedPipeClientStream(".",
            name,
            PipeDirection.InOut,
            PipeOptions.Asynchronous,
            System.Security.Principal.TokenImpersonationLevel.None,
            HandleInheritability.None);
    }

    public static NamedPipeServerStream CreateServerPipe(string name)
    {
        return new NamedPipeServerStream(name,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous, 0, 0);
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