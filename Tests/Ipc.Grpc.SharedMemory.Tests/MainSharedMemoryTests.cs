using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Ipc.Grpc.SharedMemory.Tests;

public class MainSharedMemoryTests
{
    [Test]
    public void ReadWriteIsoTest()
    {
        using MainSharedMemory sharedMemory = new("Test");
        var expectedGuid = Guid.NewGuid();
        int expectedSize = 12;

        sharedMemory.Write(expectedGuid, expectedSize);
        (Guid guid, int size) = sharedMemory.Read();

        Assert.That(guid, Is.EqualTo(expectedGuid));
        Assert.That(size, Is.EqualTo(expectedSize));
    }
    [Test]
    public void ReadWriteWithDifferentSharedMemoryTest()
    {
        using MainSharedMemory sharedMemory1 = new("Test");
        using MainSharedMemory sharedMemory2 = new("Test");
        var expectedGuid = Guid.NewGuid();
        int expectedSize = 12;

        sharedMemory1.Write(expectedGuid, expectedSize);
        (Guid guid, int size) = sharedMemory2.Read();

        Assert.That(guid, Is.EqualTo(expectedGuid));
        Assert.That(size, Is.EqualTo(expectedSize));
    }

    [Test]
    public async Task ReadAsyncWriteAsyncWithDifferentSharedMemoryTest()
    {
        using MainSharedMemory sharedMemory1 = new("Test");
        using MainSharedMemory sharedMemory2 = new("Test");
        var expectedGuid = Guid.NewGuid();
        int expectedSize = 12;

        await sharedMemory1.WriteAsync(expectedGuid, expectedSize);
        (Guid guid, int size) = await sharedMemory2.ReadAsync();

        Assert.That(guid, Is.EqualTo(expectedGuid));
        Assert.That(size, Is.EqualTo(expectedSize));
    }

    [Test]
    public void ReadAsyncShouldBlockWhenNoMessagesTest()
    {
        using MainSharedMemory sharedMemory1 = new("Test");
        CancellationTokenSource cts = new(1);
        Assert.ThrowsAsync<TaskCanceledException>(async () => await sharedMemory1.ReadAsync(cts.Token));
    }

    [Test]
    public async Task WriteAsyncShouldBlockWhenSharedMemoryIsFullTest()
    {
        using MainSharedMemory sharedMemory1 = new("Test");
        var expectedGuid = Guid.NewGuid();
        int expectedSize = 12;
        await sharedMemory1.WriteAsync(expectedGuid, expectedSize);
        CancellationTokenSource cts = new(1);
        Assert.ThrowsAsync<TaskCanceledException>(async () => await sharedMemory1.WriteAsync(expectedGuid, expectedSize, cts.Token));
    }

    [Test]
    [TestCase(10_000)]
    public async Task ReadAsyncWriteAsyncSequentialTest(int messagesCount)
    {
        using MainSharedMemory sharedMemory1 = new("Test");
        using MainSharedMemory sharedMemory2 = new("Test");


        List<(Guid guid, int size)> expectedMessages = Enumerable.Range(0, messagesCount)
                                                                 .Select(x => (guid: Guid.NewGuid(), size: x))
                                                                 .ToList();
        foreach ((Guid guid, int size) in expectedMessages)
        {
            await sharedMemory1.WriteAsync(guid, size);
            (Guid guid2, int size2) = await sharedMemory2.ReadAsync();
            Assert.That(guid2, Is.EqualTo(guid));
            Assert.That(size2, Is.EqualTo(size));
        }
    }

    [Test]
    [TestCase(10_000)]
    public async Task ReadAsyncWriteAsyncConcurrentTest(int messagesCount)
    {
        using MainSharedMemory sharedMemory1 = new("Test");
        using MainSharedMemory sharedMemory2 = new("Test");


        Task<List<(Guid guid, int size)>> writeTask = Task.Factory.StartNew(async () =>
        {
            List<(Guid guid, int size)> expectedMessages = Enumerable.Range(0, messagesCount)
                                                                     .Select(x => (guid: Guid.NewGuid(), size: x))
                                                                     .ToList();
            foreach ((Guid guid, int size) in expectedMessages)
                await sharedMemory1.WriteAsync(guid, size);
            return expectedMessages;
        }, TaskCreationOptions.LongRunning).Unwrap();

        Task<List<(Guid, int)>> readTask = Task.Factory.StartNew(async () =>
        {
            List<(Guid, int)> actualMessages = new(messagesCount);
            for (int i = 0; i < messagesCount; i++)
                actualMessages.Add(await sharedMemory2.ReadAsync());
            return actualMessages;
        }, TaskCreationOptions.LongRunning).Unwrap();
        await Task.WhenAll(writeTask, readTask);

        Assert.That(readTask.Result, Is.EquivalentTo(writeTask.Result));
    }


}
