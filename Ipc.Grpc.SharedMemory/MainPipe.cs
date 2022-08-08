#nullable enable
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace Ipc.Grpc.SharedMemory
{
    public class MainPipe : IMainMemory
    {
        private readonly PipeStream _pipeStream;

        public MainPipe(PipeStream pipeStream)
        {
            _pipeStream = pipeStream;
        }

        public async ValueTask WriteAsync(Guid guid, int size, CancellationToken token = default)
        {
            byte[] tmp = Write(guid, size);
            await _pipeStream.WriteAsync(tmp, 0, tmp.Length, token);
        }

        public async ValueTask<(Guid guid, int size)> ReadAsync(CancellationToken token = default)
        {
            byte[] tmp = new byte[20];
            int readAsync = await _pipeStream.ReadAsync(tmp, 0, tmp.Length, token);
            return Read(tmp);
        }

        public void WriteSync(Guid guid, int size)
        {
            var tmp = Write(guid, size);
            _pipeStream.Write(tmp, 0, tmp.Length);
        }

        public (Guid guid, int size) ReadSync()
        {
            byte[] tmp = new byte[20];
            int readAsync = _pipeStream.Read(tmp, 0, tmp.Length);
            return Read(tmp);
        }

        public byte[] Write(Guid guid, int size)
        {
            byte[] tmp = new byte[20];
            MemoryMarshal.Write(tmp, ref guid);
            MemoryMarshal.Write(tmp.AsSpan().Slice(16), ref size);
            return tmp;
        }

        public (Guid guid, int size) Read(byte[] buffer)
        {
            Span<byte> tmp = buffer.AsSpan();
            //Guid guid = new(tmp.Slice(0, 16));
            Guid guid = MemoryMarshal.Read<Guid>(tmp.Slice(0, 16));
            int size = MemoryMarshal.Read<int>(tmp.Slice(16));
            return (guid, size);
        }

        public void Dispose()
        {
            _pipeStream.Dispose();
        }
    }
}