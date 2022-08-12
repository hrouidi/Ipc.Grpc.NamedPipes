using System;
using System.Threading.Tasks;
using AutoFixture;
using Ipc.Grpc.NamedPipes.Tests.Helpers;
using NUnit.Framework;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal.Transport;
using Message = Ipc.Grpc.NamedPipes.Internal.Transport.Message;

namespace Ipc.Grpc.NamedPipes.Tests;

public class TransportTests
{
    [Test]
    public void FrameHeader_Tests()
    {
        Span<byte> bytes = stackalloc byte[8];
        NamedPipeTransport.FrameHeader.Write(bytes, 15, 8);
        NamedPipeTransport.FrameHeader header2 = NamedPipeTransport.FrameHeader.Parse(bytes);
        Assert.That(15, Is.EqualTo(header2.MessageSize));
        Assert.That(8, Is.EqualTo(header2.PayloadSize));
        Assert.That(23, Is.EqualTo(header2.TotalSize));
    }


    [Test]
    public async Task Frames_WithoutPayload_FullRoadTrip_Test()
    {
        //Arrange
        using var channel = PipeChannel.CreateRandom();
        Fixture fixture = new();
        var expectedRequest = fixture.Create<Message>();
        var expectedResponse = fixture.Create<Message>();

        using var clientTransport = new NamedPipeTransport(channel.ClientStream);
        using var serverTransport = new NamedPipeTransport(channel.ServerStream);

        //Act
        var readRequestTask = serverTransport.ReadFrame();
        await clientTransport.SendFrame(expectedRequest);
        Message frame = await readRequestTask;
        //Assert
        Assert.That(frame, Is.EqualTo(expectedRequest));
        //Act
        var readResponseTask = clientTransport.ReadFrame();
        await serverTransport.SendFrame(expectedResponse);
        Message serverFrame = await readResponseTask;
        //Assert
        Assert.That(serverFrame, Is.EqualTo(expectedResponse));
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
}