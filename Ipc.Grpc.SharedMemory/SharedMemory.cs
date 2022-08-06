#nullable enable
using System.IO.MemoryMappedFiles;

namespace Ipc.Grpc.SharedMemory
{
    public class SharedMemory : IDisposable
    {
        private const string _readSemaphorePrefix = "AAD369E4-23A8-43AE-AF9F-8AD09528CF7F";
        private const string _writeSemaphorePrefix = "B1D8B82B-3F20-496A-9AB1-4CC3D684B4A5";

        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;


        private readonly Semaphore _writeSemaphore;
        private readonly Semaphore _readSemaphore;

        public SharedMemory(string name)
        {
            _readSemaphore = new Semaphore(0, int.MaxValue, $"{_readSemaphorePrefix}{name}", out bool isNewRead);
            _writeSemaphore = new Semaphore(1, int.MaxValue, $"{_writeSemaphorePrefix}{name}", out bool isNewWrite);

            _mmf = MemoryMappedFile.CreateOrOpen(name, 4096);
            _accessor = _mmf.CreateViewAccessor();
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