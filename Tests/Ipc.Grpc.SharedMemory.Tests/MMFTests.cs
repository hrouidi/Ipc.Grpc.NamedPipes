using System;
using System.Buffers;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Ipc.Grpc.SharedMemory.Tests;

public class MmfTests
{
    [Test]
    public async Task MmfPocTest()
    {
        Semaphore sem = new(0, 100, "test0", out bool isNew);
        Mutex mutex = new(true, "test1");
        EventWaitHandle ewh = new(true, EventResetMode.AutoReset, "test2");

        using var mmf = MemoryMappedFile.CreateNew("test", 1024 * 1024 * 1024); // MemoryMappedFileAccess.Write, MemoryMappedFileOptions.None, System.IO.HandleInheritability.Inheritable);
        using MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor();
        var buffer1 = new byte[1024];
        for (int i = 0; i < 1024 * 1024; i++)
        {
            Random.Shared.NextBytes(buffer1);
            accessor.WriteArray(0, buffer1, 0, 1024);
            //sem.Release();
        }

        using var mmf2 = MemoryMappedFile.OpenExisting("test");
        using MemoryMappedViewAccessor accessor2 = mmf.CreateViewAccessor();
        //var buffer = new byte[1024];
        //int count = accessor.ReadArray(0, buffer, 0, 1024);

        IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(1024);
        Memory<byte> messageBytes = owner.Memory[..1024];
        var ret = await sem.WaitAsync(TimeSpan.FromSeconds(10));
        int cpt2 = await accessor2.ReadAsync(0, messageBytes);
    }


    [Test]
    public async Task WaitAsync_Tests()
    {
        Semaphore semaphore = new(0, int.MaxValue, "test0", out bool isNew);

        semaphore.Release();
        semaphore.Release();
        semaphore.WaitOne();
        semaphore.WaitOne();

        semaphore.Release();
        semaphore.Release();

        var reg1 = ThreadPool.RegisterWaitForSingleObject(semaphore, static (state, timedOut) => Console.WriteLine("001"), null, TimeSpan.FromSeconds(10), executeOnlyOnce: true);
        var reg2 = ThreadPool.RegisterWaitForSingleObject(semaphore, static (state, timedOut) => Console.WriteLine("002"), null, TimeSpan.FromSeconds(10), executeOnlyOnce: true);
        reg1.Unregister(null);
        reg2.Unregister(null);
        //var ret1 = await semaphore.WaitAsync(TimeSpan.FromSeconds(10));
        //var ret2 = await semaphore.WaitAsync(TimeSpan.FromSeconds(10));

        var ret3 = await semaphore.WaitAsync(TimeSpan.FromSeconds(10));

    }

}
