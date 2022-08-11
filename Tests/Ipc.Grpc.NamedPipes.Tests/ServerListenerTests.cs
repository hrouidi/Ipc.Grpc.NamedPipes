using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using Ipc.Grpc.NamedPipes.Internal;
using Ipc.Grpc.NamedPipes.Tests.Helpers;
using NUnit.Framework;

namespace Ipc.Grpc.NamedPipes.Tests;

public class ServerListenerTests
{
    [Test]
    public async Task Start_Stop_Tests()
    {
        using var pool = new ServerListener($"{Guid.NewGuid()}", NamedPipeServerOptions.Default, new Dictionary<string, Func<ServerConnection, ValueTask>>());

        await pool.StartAsync();
        await pool.ShutdownAsync();
        await pool.ShutdownAsync();

        await pool.StartAsync();
        await pool.StartAsync();
        await pool.ShutdownAsync();
    }

    [Test]
    public async Task Dispose_Tests()
    {
        var pool = new ServerListener($"{Guid.NewGuid()}", NamedPipeServerOptions.Default, new Dictionary<string, Func<ServerConnection, ValueTask>>());
        pool.Dispose();
        Assert.ThrowsAsync<ObjectDisposedException>(pool.StartAsync);
        Assert.ThrowsAsync<ObjectDisposedException>(pool.ShutdownAsync);

        var pool2 = new ServerListener($"{Guid.NewGuid()}", NamedPipeServerOptions.Default, new Dictionary<string, Func<ServerConnection, ValueTask>>());
        await pool2.StartAsync();
        pool2.Dispose();
        Assert.ThrowsAsync<ObjectDisposedException>(pool2.StartAsync);
        Assert.ThrowsAsync<ObjectDisposedException>( pool2.ShutdownAsync);

        var pool3 = new ServerListener($"{Guid.NewGuid()}", NamedPipeServerOptions.Default, new Dictionary<string, Func<ServerConnection, ValueTask>>());
        await pool3.StartAsync();
        pool3.Dispose();
        pool3.Dispose();
        pool3.Dispose();
    }

    [Test]
    [TestCase(1000, 1)]
    public async Task Throughput_Tests(int clientCount, int connectionTimeoutMs)
    {
        var pipeName = $"{Guid.NewGuid()}";

        using var pool = new ServerListener(pipeName, NamedPipeServerOptions.Default, new Dictionary<string, Func<ServerConnection, ValueTask>>());
        await pool.StartAsync();
        //await Task.Delay(10);

        List<Task> clients = new List<Task>(clientCount);
        foreach (var client in Enumerable.Range(0, clientCount).Select(x => PipeChannel.CreateClientPipe(pipeName)))
            clients.Add(client.ConnectAsync(connectionTimeoutMs));
        //for (int i = 0; i < clientCount; i++)
        //{
        //    NamedPipeClientStream client = PipeChannel.CreateClientPipe(pipeName);
        //    clients.Add(client.ConnectAsync(connectionTimeoutMs));
        //    //await Task.Delay(1);
        //}
        try
        {
            await Task.WhenAll(clients.ToArray());
        }
        catch (Exception e)
        {
            //Console.WriteLine(e.ToString());
        }
        finally
        {
            Console.WriteLine($"Connected: {clients.Count(x => x.IsCompleted)}/{clientCount}");
            Console.WriteLine($"Faulted: {clients.Count(x=>x.IsFaulted)}/{clientCount}");
            //Console.WriteLine($"Connected: {clients.Where(x=>x.Status =)}/{clientCount}");
        }
        
    }

    [Test, Explicit]
    public void Debug_Tests()
    {
        var channel = PipeChannel.CreateRandom();
        channel.ServerStream.Disconnect();
        Task.WaitAll(channel.ClientStream.ConnectAsync(), channel.ServerStream.WaitForConnectionAsync());
    }
}