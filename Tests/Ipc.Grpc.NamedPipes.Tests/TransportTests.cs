using System;
using System.Threading.Tasks;
using AutoFixture;
using Ipc.Grpc.NamedPipes.Internal;
using Ipc.Grpc.NamedPipes.Tests.Helpers;
using Ipc.Grpc.NamedPipes.TransportProtocol;
using NUnit.Framework;
using Grpc.Core;

namespace Ipc.Grpc.NamedPipes.Tests;

public class TransportTests
{
    [Test]
    public void FrameHeader_Tests()
    {
        Span<byte> bytes = stackalloc byte[8];
        Transport.FrameHeader header1 = new(15, 8);
        Transport.FrameHeader.Write(bytes, 15, 8);
        Transport.FrameHeader header2 = Transport.FrameHeader.FromSpan(bytes);
        Assert.That(header1, Is.EqualTo(header2));
    }


    [Test]
    public async Task Frames_WithoutPayload_FullRoadTrip_Test()
    {
        //Arrange
        using var channel = PipeChannel.CreateRandom();
        Fixture fixture = new();
        var expectedRequest = fixture.Create<Message>();
        var expectedResponse = fixture.Create<Message>();

        using var clientTransport = new Transport(channel.ClientStream);
        using var serverTransport = new Transport(channel.ServerStream);

        //Act
        var readRequestTask = serverTransport.ReadFrame();
        await clientTransport.SendFrame(expectedRequest);
        Frame frame = await readRequestTask;
        //Assert
        Assert.That(frame.Message, Is.EqualTo(expectedRequest));
        //Act
        var readResponseTask = clientTransport.ReadFrame();
        await serverTransport.SendFrame(expectedResponse);
        Frame serverFrame = await readResponseTask;
        //Assert
        Assert.That(serverFrame.Message, Is.EqualTo(expectedResponse));
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

        using var clientTransport = new Transport(channel.ClientStream);
        using var serverTransport = new Transport(channel.ServerStream);

        //Act
        var readRequestTask = serverTransport.ReadFrame();
        var requestInfo = new FrameInfo<string>(expectedRequest, expectedRequestPayload, Marshallers.StringMarshaller.ContextualSerializer);
        await clientTransport.SendFrame(requestInfo);
        Frame? frame = await readRequestTask;
        //Assert
        Assert.That(frame.Message, Is.EqualTo(expectedRequest));
        CollectionAssert.AreEqual(frame.GetPayload(Marshallers.StringMarshaller.ContextualDeserializer), expectedRequestPayload);
        
        //Act
        var readResponseTask = clientTransport.ReadFrame();
        var responseInfo = new FrameInfo<string>(expectedRequest, expectedResponsePayload, Marshallers.StringMarshaller.ContextualSerializer);
        await serverTransport.SendFrame(responseInfo);
        var serverFrame = await readResponseTask;
        //Assert
        Assert.That(serverFrame.Message, Is.EqualTo(expectedResponse));
        CollectionAssert.AreEqual(serverFrame.GetPayload(Marshallers.StringMarshaller.ContextualDeserializer), expectedResponsePayload);
    }
}