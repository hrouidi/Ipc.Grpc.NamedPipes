using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Ipc.Grpc.NamedPipes.Internal.Helpers
{
    public static class StreamExtension
    {
        public static ValueTask<int> ReadAsync(this PipeStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment) == false)
                throw new NotSupportedException("Array-based buffer required");

            return new ValueTask<int>(stream.ReadAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken));
        }

        public static ValueTask WriteAsync(this PipeStream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment) == false)
                throw new NotSupportedException("Array-based buffer required");
            return new ValueTask(stream.WriteAsync(segment.Array, segment.Offset, segment.Count, cancellationToken));
        }
    }
}