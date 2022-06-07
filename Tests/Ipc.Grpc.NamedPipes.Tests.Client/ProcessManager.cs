using System;
using System.Diagnostics;

namespace Ipc.Grpc.NamedPipes.Tests.Client;

public class ProcessManager : IDisposable
{
    private readonly Process _process;
    public ProcessManager(string exePath)
    {
        _process = new Process()
        {
            StartInfo = new ProcessStartInfo(exePath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            }
        };
        _process.OutputDataReceived += Process_OutputDataReceived;
        _process.ErrorDataReceived += Process_ErrorDataReceived;
        _process.Exited += Process_Exited;
        _process.EnableRaisingEvents = true;
        if (_process.Start() == false)
            throw new Exception("Can not start process");
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public void Dispose()
    {
        _process.Kill();
        _process.Dispose();
    }

    private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        Console.WriteLine($"Process output :{e.Data}");
    }

    private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        Console.WriteLine($"Process error :{e.Data}");
    }

    private void Process_Exited(object? sender, System.EventArgs e)
    {
        Console.WriteLine("Process exited");
    }


}