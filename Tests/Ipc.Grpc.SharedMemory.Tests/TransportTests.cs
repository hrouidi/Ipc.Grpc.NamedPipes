using System;
using System.Threading.Tasks;
using AutoFixture;
using NUnit.Framework;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal.Transport;
using Ipc.Grpc.SharedMemory;
using Message = Ipc.Grpc.NamedPipes.Internal.Transport.Message;

namespace Ipc.Grpc.NamedPipes.Tests;

public class TransportTests
{

    //[Test]
    //public async Task Frames_WithoutPayload_FullRoadTrip_Test()
    //{
    //    //Arrange
    //    Fixture fixture = new();
    //    var expectedRequest = fixture.Create<Message>();
    //    var expectedResponse = fixture.Create<Message>();

    //    using var clientTransport = new SharedMemoryTransport(new MainSharedMemory("test"));
    //    using var serverTransport = new SharedMemoryTransport(new MainSharedMemory("test"));

    //    //Act
    //    var readRequestTask = serverTransport.ReadFrame();
    //    await clientTransport.SendFrame(expectedRequest);
    //    Message frame = await readRequestTask;
    //    //Assert
    //    Assert.That(frame, Is.EqualTo(expectedRequest));
    //    //Act
    //    var readResponseTask = clientTransport.ReadFrame();
    //    await serverTransport.SendFrame(expectedResponse);
    //    Message serverFrame = await readResponseTask;
    //    //Assert
    //    Assert.That(serverFrame, Is.EqualTo(expectedResponse));
    //}


    //[Test]
    //public async Task Frames_Payload_FullRoadTrip_Test()
    //{
    //    //Arrange
    //    Random random = new();
    //    Fixture fixture = new();
    //    var expectedRequest = fixture.Create<Message>();
    //    string expectedRequestPayload = "expectedRequestPayload";
    //    var expectedResponse = fixture.Create<Message>();
    //    string expectedResponsePayload = "expectedResponsePayload";

    //    using var clientTransport = new SharedMemoryTransport(new MainSharedMemory("test"));
    //    using var serverTransport = new SharedMemoryTransport(new MainSharedMemory("test"));

    //    //Act
    //    var readRequestTask = serverTransport.ReadFrame();
    //    var requestInfo = new MessageInfo<string>(expectedRequest, expectedRequestPayload, Marshallers.StringMarshaller.ContextualSerializer);
    //    await clientTransport.SendFrame(requestInfo);
    //    Message? frame = await readRequestTask;
    //    //Assert
    //    Assert.That(frame, Is.EqualTo(expectedRequest));
    //    CollectionAssert.AreEqual(frame.GetPayload(Marshallers.StringMarshaller.ContextualDeserializer), expectedRequestPayload);

    //    //Act
    //    var readResponseTask = clientTransport.ReadFrame();
    //    var responseInfo = new MessageInfo<string>(expectedRequest, expectedResponsePayload, Marshallers.StringMarshaller.ContextualSerializer);
    //    await serverTransport.SendFrame(responseInfo);
    //    var serverFrame = await readResponseTask;
    //    //Assert
    //    Assert.That(serverFrame, Is.EqualTo(expectedResponse));
    //    CollectionAssert.AreEqual(serverFrame.GetPayload(Marshallers.StringMarshaller.ContextualDeserializer), expectedResponsePayload);
    //}
}