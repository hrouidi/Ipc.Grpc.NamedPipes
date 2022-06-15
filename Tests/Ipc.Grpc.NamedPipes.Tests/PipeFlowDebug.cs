using System;
using System.Collections.Generic;
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

    [Test]
    public async Task Frames_WithoutPayload_FullRoadTrip_Test()
    {
        //Arrange
        var channel = PipeChannel.CreateRandom();
        byte[] buffer = new byte[8];
        byte[] SendBsffer = new byte[8] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var task = channel.ClientStream.ReadAsync(buffer, 0, 8);
        await channel.ServerStream.WriteAsync(SendBsffer, 0, 8);
        channel.ServerStream.Disconnect();
        var read = await task;
    }


    [Test]
    public async Task Frames_Payload_FullRoadTrip_Test()
    {
        //Arrange
        using var channel = PipeChannel.CreateRandom();
        Random random = new();
        Fixture fixture = new();
        var expectedRequest = fixture.Create<Message>();
        string expectedRequestPayload = "expectedRequestPayload";
        var expectedResponse = fixture.Create<Message>();
        string expectedResponsePayload = "expectedResponsePayload";

        using var clientTransport = new NamedPipeTransport(channel.ClientStream);
        using var serverTransport = new NamedPipeTransport(channel.ServerStream);

        //Act
        var readRequestTask = serverTransport.ReadFrame();
        var requestInfo = new MessageInfo<string>(expectedRequest, expectedRequestPayload, Marshallers.StringMarshaller.ContextualSerializer);
        await clientTransport.SendFrame(requestInfo);
        Message? frame = await readRequestTask;
        //Assert
        Assert.That(frame, Is.EqualTo(expectedRequest));
        CollectionAssert.AreEqual(frame.GetPayload(Marshallers.StringMarshaller.ContextualDeserializer), expectedRequestPayload);

        //Act
        var readResponseTask = clientTransport.ReadFrame();
        var responseInfo = new MessageInfo<string>(expectedRequest, expectedResponsePayload, Marshallers.StringMarshaller.ContextualSerializer);
        await serverTransport.SendFrame(responseInfo);
        var serverFrame = await readResponseTask;
        //Assert
        Assert.That(serverFrame, Is.EqualTo(expectedResponse));
        CollectionAssert.AreEqual(serverFrame.GetPayload(Marshallers.StringMarshaller.ContextualDeserializer), expectedResponsePayload);
    }

    [Test]
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