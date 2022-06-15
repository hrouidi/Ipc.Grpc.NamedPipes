using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Ipc.Grpc.NamedPipes.Internal.Helpers;

public static class StreamExtension
{
    public static Task<int> ReadAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment) == false)
            throw new NotSupportedException("Array-based buffer required");

        return stream.ReadAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken);
    }
    public static Task WriteAsync(this Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment) == false)
            throw new NotSupportedException("Array-based buffer required");

        return stream.WriteAsync(segment.Array, segment.Offset, segment.Count, cancellationToken);
    }
}