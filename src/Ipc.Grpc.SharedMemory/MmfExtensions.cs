using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Sources;

namespace Ipc.Grpc.SharedMemory
{

    public static class MmfExtensions
    {
        public static unsafe Span<byte> GetSpan(this MemoryMappedViewAccessor accessor)
        {
            byte* ptr = null;
            try
            {
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                Span<byte> buffer2 = new(ptr, (int)accessor.Capacity);
                return buffer2;
            }
            finally
            {
                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            }
        }

        public static ValueTask<int> ReadAsync(this MemoryMappedViewAccessor accessor, long position, Memory<byte> buffer, CancellationToken token = default)
        {
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment) == false)
                throw new NotSupportedException("Array-based buffer required");
            int count = accessor.ReadArray(position, segment.Array!, segment.Offset, segment.Count);
            return new ValueTask<int>(count);
        }
    }
}