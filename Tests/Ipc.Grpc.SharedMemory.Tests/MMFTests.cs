using System;
using System.Buffers;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Ipc.Grpc.SharedMemory.Helpers;
using NUnit.Framework;

namespace Ipc.Grpc.SharedMemory.Tests;

public class MmfTests
{
    [Test]
    public void MmfPocTest()
    {
        //Semaphore sem = new(0, 100, "test0", out bool isNew);
        //Mutex mutex = new(true, "test1");
        //EventWaitHandle ewh = new(true, EventResetMode.AutoReset, "test2");

        Span<byte> span1;

        using MemoryMappedFile mmf = MemoryMappedFile.CreateNew("test", 1024 * 1024 * 1024); // MemoryMappedFileAccess.Write, MemoryMappedFileOptions.None, System.IO.HandleInheritability.Inheritable);
        using MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor();
        span1 = accessor.GetSpan();
        RandomNumberGenerator.Fill(span1);

        //accessor.Dispose();

        using MemoryMappedFile mmf2 = MemoryMappedFile.OpenExisting("test");
        mmf.Dispose();
        accessor.Dispose();
        using MemoryMappedViewAccessor accessor2 = mmf2.CreateViewAccessor();
        var span2 = accessor2.GetSpan();

        Assert.That(span1.SequenceEqual(span2));
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

    [Test]
    public static void Debug()
    {
        using var mmf = MemoryMappedFile.CreateNew("test", 1024);

        using MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor();

        Span<byte> viewSpan = accessor.GetSpan();

        var buffer1 = new byte[1024 * 4];
        Random.Shared.NextBytes(viewSpan);
        // accessor.WriteArray(0, buffer1, 0, 1024);


        using var mmf2 = MemoryMappedFile.OpenExisting("test");
        using MemoryMappedViewAccessor accessor2 = mmf2.CreateViewAccessor();
        var buffer = new byte[1024];
        int count = accessor2.ReadArray(0, buffer, 0, 1024);

        //ref MemoryMappedViewAccessor tmp = ref Unsafe.AsRef(accessor);


    }
}
