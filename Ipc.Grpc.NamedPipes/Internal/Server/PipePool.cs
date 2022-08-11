using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Linq;

namespace Ipc.Grpc.NamedPipes.Internal
{
    public interface IPipePool : IDisposable
    {
        NamedPipeServerStream Get();
        void AddNew();
    }

    public class FakePipePool : IPipePool
    {
        private readonly Func<NamedPipeServerStream> _pipeFactory;

        public FakePipePool(Func<NamedPipeServerStream> objectGenerator)
        {
            _pipeFactory = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
        }

        public NamedPipeServerStream Get() => _pipeFactory();

        public void AddNew() {}

        public void Dispose()
        {
        }
    }

    public class PipePool : IPipePool
    {
        private readonly ConcurrentQueue<NamedPipeServerStream> _pipes;
        private readonly Func<NamedPipeServerStream> _pipeFactory;

        public PipePool(Func<NamedPipeServerStream> objectGenerator, int poolSize )
        {
            _pipeFactory = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
            _pipes = new ConcurrentQueue<NamedPipeServerStream>(Enumerable.Range(0, poolSize).Select(x => _pipeFactory()));
        }

        public NamedPipeServerStream Get() => _pipes.TryDequeue(out NamedPipeServerStream item) ? item : _pipeFactory();

        public void AddNew() => _pipes.Enqueue(_pipeFactory());

        public void Dispose()
        {
            foreach (NamedPipeServerStream pipe in _pipes)
                SafeDispose(pipe);
        }

        private void SafeDispose(IDisposable pipe)
        {
            try
            {
                pipe.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{nameof(PipePool)}: disposing pipes ,[Pool size]={_pipes.Count}, [Error] ={ex.Message}");
            }
        }
    }
}