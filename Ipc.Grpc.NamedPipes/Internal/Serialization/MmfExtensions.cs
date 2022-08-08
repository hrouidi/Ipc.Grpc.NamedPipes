using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Ipc.Grpc.NamedPipes.Internal
{
    public static class MmfExtensions
    {
        public static Span<byte> GetSpan(this MemoryMappedFile mmf, int size)
        {
            using MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(0, size);
            return accessor.GetSpan();
        }
        //public static unsafe Span<byte> GetSpan(this MemoryMappedFile mmf, int size)
        //{
        //    try
        //    {
        //        var ptr = (byte*)mmf.SafeMemoryMappedFileHandle.DangerousGetHandle().ToPointer();
        //        Span<byte> buffer2 = new(ptr, size);
        //        return buffer2;
        //    }
        //    finally
        //    {
        //        mmf.SafeMemoryMappedFileHandle.DangerousRelease();
        //    }
        //}

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

        //public static ValueTask<int> ReadAsync(this MemoryMappedViewAccessor accessor, long position, Memory<byte> buffer, CancellationToken token = default)
        //{
        //    if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment) == false)
        //        throw new NotSupportedException("Array-based buffer required");
        //    int count = accessor.ReadArray(position, segment.Array!, segment.Offset, segment.Count);
        //    return new ValueTask<int>(count);
        //}
    }
}