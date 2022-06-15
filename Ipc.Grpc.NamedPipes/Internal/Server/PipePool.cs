using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Linq;

namespace Ipc.Grpc.NamedPipes.Internal;

public class PipePool : IDisposable
{
    private readonly ConcurrentBag<NamedPipeServerStream> _pipes;
    private readonly Func<NamedPipeServerStream> _pipeFactory;

    public PipePool(Func<NamedPipeServerStream> objectGenerator, int initSize = 1024)
    {
        _pipeFactory = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
        _pipes = new ConcurrentBag<NamedPipeServerStream>(Enumerable.Range(0, initSize).Select(x => _pipeFactory()));
    }

    public NamedPipeServerStream Get() => _pipes.TryTake(out NamedPipeServerStream item) ? item : _pipeFactory();

    public void Return(NamedPipeServerStream item) => _pipes.Add(item);

    public void AddNew() => _pipes.Add(_pipeFactory());

    public void Dispose()
    {
        foreach (NamedPipeServerStream pipe in _pipes)
            SafeDispose(pipe);
    }

    private static void SafeDispose(NamedPipeServerStream pipe)
    {
        try
        {
            pipe.Dispose();
        }
        catch
        {

        }
    }
}