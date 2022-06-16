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
    public void Start_Stop_Tests()
    {
        using var pool = new ServerListener($"{Guid.NewGuid()}", NamedPipeServerOptions.Default, new Dictionary<string, Func<ServerConnection, ValueTask>>());

        pool.Start();
        pool.Stop();
        pool.Stop();

        pool.Start();
        pool.Start();
        pool.Stop();
    }

    [Test]
    public void Dispose_Tests()
    {
        var pool = new ServerListener($"{Guid.NewGuid()}", NamedPipeServerOptions.Default, new Dictionary<string, Func<ServerConnection, ValueTask>>());
        pool.Dispose();
        Assert.Throws<ObjectDisposedException>(() => pool.Start());
        Assert.Throws<ObjectDisposedException>(() => pool.Stop());

        var pool2 = new ServerListener($"{Guid.NewGuid()}", NamedPipeServerOptions.Default, new Dictionary<string, Func<ServerConnection, ValueTask>>());
        pool2.Start();
        pool2.Dispose();
        Assert.Throws<ObjectDisposedException>(() => pool2.Start());
        Assert.Throws<ObjectDisposedException>(() => pool2.Stop());

        var pool3 = new ServerListener($"{Guid.NewGuid()}", NamedPipeServerOptions.Default, new Dictionary<string, Func<ServerConnection, ValueTask>>());
        pool3.Start();
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
        pool.Start();
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