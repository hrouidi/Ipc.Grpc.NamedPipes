using System.IO.MemoryMappedFiles;
using NUnit.Framework;

namespace Ipc.Grpc.NamedPipes.Tests;

public class MMFTests
{
    [Test]
    
    public void MmfPocTest()
    {
        using var mmf = MemoryMappedFile.CreateNew("test", 1024);// MemoryMappedFileAccess.Write, MemoryMappedFileOptions.None, System.IO.HandleInheritability.Inheritable);
        using MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor();
        accessor.WriteArray(0, new byte[1024], 0, 1024);
        using var mmf2 = MemoryMappedFile.OpenExisting("test");
        using MemoryMappedViewAccessor accessor2 = mmf.CreateViewAccessor();
        var buffer = new byte[1024];
        int count = accessor.ReadArray(0, buffer, 0, 1024);
    }
}