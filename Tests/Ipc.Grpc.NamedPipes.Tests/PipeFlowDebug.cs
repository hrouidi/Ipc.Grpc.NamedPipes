using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using Ipc.Grpc.NamedPipes.Tests.Helpers;
using NUnit.Framework;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal.Transport;
using JetBrains.dotMemoryUnit;
using Message = Ipc.Grpc.NamedPipes.Internal.Transport.Message;

namespace Ipc.Grpc.NamedPipes.Tests;

public class PipeFlowDebug
{

    [Test, Explicit]
    public async Task WriteAsync_is_blocking()
    {
        //Arrange
        var channel = PipeChannel.CreateRandom();
        byte[] buffer = new byte[8];
        byte[] sendBuffer = new byte[8] { 1, 2, 3, 4, 5, 6, 7, 8 };

        channel.ClientStream.ReadMode = PipeTransmissionMode.Message;
        await channel.ClientStream.WriteAsync(sendBuffer, 0, 8);
        var task = channel.ServerStream.ReadAsync(buffer, 0, 8);

        var reader = new StreamReader(channel.ServerStream);
        //reader.

    }

    [Test, Explicit]
    public void Debug()
    {
        string pipeName = Guid.NewGuid().ToString();

        List<NamedPipeServerStream> pipes = Enumerable.Range(0, 1024)
                                                      .Select(x => PipeChannel.CreateServerPipe(pipeName))
                                                      .ToList();


        dotMemory.Check(memory =>
        {
            Console.WriteLine($"Count : {memory.GetObjects(y => y.Type.Is<NamedPipeServerStream>()).ObjectsCount}");
            Console.WriteLine($"Size : {memory.GetObjects(y => y.Type.Is<NamedPipeServerStream>()).SizeInBytes}");
            Assert.That(memory.GetObjects(y => y.Type.Is<NamedPipeServerStream>()).ObjectsCount == 1);
        });

        foreach (NamedPipeServerStream? pipe in pipes)
            pipe.Dispose();

    }
}