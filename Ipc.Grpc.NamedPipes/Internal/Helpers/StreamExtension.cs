using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Ipc.Grpc.NamedPipes.Internal.Helpers;

public static class StreamExtension
{
    public static ValueTask<int> ReadAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (MemoryMarshal.TryGetArray<byte>(buffer, out var segment))
            return new ValueTask<int>(stream.ReadAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken));
        throw new NotSupportedException("Array-based buffer required");
    }
    public static ValueTask WriteAsync(this Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (MemoryMarshal.TryGetArray(buffer, out var segment))
            return new ValueTask(stream.WriteAsync(segment.Array, segment.Offset, segment.Count, cancellationToken));
        throw new NotSupportedException("Array-based buffer required");
    }
}