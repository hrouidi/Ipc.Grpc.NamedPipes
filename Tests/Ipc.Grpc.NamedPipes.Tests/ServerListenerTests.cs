using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading.Tasks;
using Ipc.Grpc.NamedPipes.Internal;
using Ipc.Grpc.NamedPipes.Tests.Helpers;
using NUnit.Framework;

namespace Ipc.Grpc.NamedPipes.Tests;

public class ServerListenerTests
{
    [Test]
    public void Start_Stop_Tests()
    {
        using var pool = new ServerListener($"{Guid.NewGuid()}", NamedPipeServerOptions.Default, new Dictionary<string, Func<ServerConnectionContext, ValueTask>>());

        pool.Start(1);
        pool.Stop();
        pool.Stop();

        pool.Start(10);
        pool.Start(10);
        pool.Stop();
    }

    [Test]
    public void Dispose_Tests()
    {
        var pool = new ServerListener($"{Guid.NewGuid()}", NamedPipeServerOptions.Default, new Dictionary<string, Func<ServerConnectionContext, ValueTask>>());
        pool.Dispose();
        Assert.Throws<ObjectDisposedException>(() => pool.Start());
        Assert.Throws<ObjectDisposedException>(() => pool.Stop());

        var pool2 = new ServerListener($"{Guid.NewGuid()}", NamedPipeServerOptions.Default, new Dictionary<string, Func<ServerConnectionContext, ValueTask>>());
        pool2.Start();
        pool2.Dispose();
        Assert.Throws<ObjectDisposedException>(() => pool2.Start());
        Assert.Throws<ObjectDisposedException>(() => pool2.Stop());

        var pool3 = new ServerListener($"{Guid.NewGuid()}", NamedPipeServerOptions.Default, new Dictionary<string, Func<ServerConnectionContext, ValueTask>>());
        pool3.Start();
        pool3.Dispose();
        pool3.Dispose();
        pool3.Dispose();
    }

    [Test]
    [TestCase(10, 1)]
    public async Task Throughput_Tests(int clientCount, int connectionTimeoutMs)
    {
        var pipeName = $"{Guid.NewGuid()}";

        using var pool = new ServerListener(pipeName, NamedPipeServerOptions.Default, new Dictionary<string, Func<ServerConnectionContext, ValueTask>>());
        pool.Start(9);
        //await Task.Delay(10);

        List<Task> clients = new List<Task>(clientCount);
        for (int i = 0; i < clientCount; i++)
        {
            NamedPipeClientStream client = PipeChannel.CreateClientPipe(pipeName);
            clients.Add(client.ConnectAsync(connectionTimeoutMs));
            //await Task.Delay(1000);
        }
        await Task.WhenAll(clients.ToArray());
    }
}