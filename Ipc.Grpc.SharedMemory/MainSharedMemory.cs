#nullable enable
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using Ipc.Grpc.SharedMemory.Helpers;

namespace Ipc.Grpc.SharedMemory
{
    public interface IMainMemory : IDisposable
    {
        ValueTask WriteAsync(Guid guid, int size, CancellationToken token = default);
        ValueTask<(Guid guid, int size)> ReadAsync(CancellationToken token = default);

        void WriteSync(Guid guid, int size);

        (Guid guid, int size) ReadSync();
    }

    public class MainSharedMemory : IMainMemory
    {
        private const string _readSemaphorePrefix = "AAD369E4-23A8-43AE-AF9F-8AD09528CF7F";
        private const string _writeSemaphorePrefix = "B1D8B82B-3F20-496A-9AB1-4CC3D684B4A5";

        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;


        private readonly Semaphore _writeSemaphore;
        private readonly Semaphore _readSemaphore;

        public MainSharedMemory(string name)
        {
            _readSemaphore = new Semaphore(0, int.MaxValue, $"{_readSemaphorePrefix}{name}", out bool isNewRead);
            _writeSemaphore = new Semaphore(1, int.MaxValue, $"{_writeSemaphorePrefix}{name}", out bool isNewWrite);

            _mmf = MemoryMappedFile.CreateOrOpen(name, 4096);
            _accessor = _mmf.CreateViewAccessor(0, 16 + 4);
        }

        public async ValueTask WriteAsync(Guid guid, int size, CancellationToken token = default)
        {
            _writeSemaphore.WaitOne();
            //await _writeSemaphore.WaitAsync(token).ConfigureAwait(false);
            Write(guid, size);
            _readSemaphore.Release();
        }

        public void WriteSync(Guid guid, int size)
        {
            _writeSemaphore.WaitOne();
            Write(guid, size);
            _readSemaphore.Release();
        }

        public async ValueTask<(Guid guid, int size)> ReadAsync(CancellationToken token = default)
        {
            //await _readSemaphore.WaitAsync(token).ConfigureAwait(false);
            _readSemaphore.WaitOne();
            (Guid guid, int size) ret = Read();
            _writeSemaphore.Release();
            return ret;
        }
        
        public (Guid guid, int size) ReadSync()
        {
            _readSemaphore.WaitOne();
            (Guid guid, int size) ret = Read();
            _writeSemaphore.Release();
            return ret;
        }

        public void Write(Guid guid, int size)
        {
            Span<byte> tmp = _accessor.GetSpan();
            MemoryMarshal.Write(tmp, ref guid);
            MemoryMarshal.Write(tmp.Slice(16), ref size);
        }

        public (Guid guid, int size) Read()
        {
            Span<byte> tmp = _accessor.GetSpan();
            var guid = MemoryMarshal.Read<Guid>(tmp.Slice(0,16));
            int size = MemoryMarshal.Read<int>(tmp.Slice(16));
            return (guid, size);
        }

        public void Dispose()
        {
            _accessor.Dispose();
            _mmf.Dispose();
            _readSemaphore.Dispose();
            _writeSemaphore.Dispose();
        }
    }
}