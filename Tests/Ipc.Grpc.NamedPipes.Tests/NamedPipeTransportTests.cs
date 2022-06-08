using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using Ipc.Grpc.NamedPipes.Internal;
using Ipc.Grpc.NamedPipes.Tests.Helpers;
using Ipc.Grpc.NamedPipes.TransportProtocol;
using NUnit.Framework;
using Google.Protobuf;

namespace Ipc.Grpc.NamedPipes.Tests;

public class NamedPipeTransportTests
{
    [Test]
    public void FrameHeader_Tests()
    {
        Span<byte> layout = stackalloc byte[8];
        Span<byte> layout2 = stackalloc byte[8];
        var bytes = new byte[8];
        var ret = NamedPipeTransportV2.FrameHeader.FromSpan(bytes);
        NamedPipeTransportV2.FrameHeader.ToSpan(layout, ref ret);

        NamedPipeTransportV2.FrameHeader h = new(15, 8);
        NamedPipeTransportV2.FrameHeader.ToSpan3(layout2, 15,8);
        var h2 = NamedPipeTransportV2.FrameHeader.FromSpan(layout2);
        Assert.That(h, Is.EqualTo(h2));
    }

    [Test]
    public async Task SendFrame2_Test()
    {
        using var channel = PipeChannel.CreateRandom();
        Fixture fixture = new();
        var expectedRequest = fixture.Create<Frame>();
        var expectedResponse = fixture.Create<Frame>();

        Random random = new();
        byte[] expectedRequestPayload = new byte[100];
        random.NextBytes(expectedRequestPayload);

        using var clientTransport = new NamedPipeTransportV2(channel.ClientStream);
        using var serverTransport = new NamedPipeTransportV2(channel.ServerStream);

        var readRequestTask = serverTransport.ReadFrame();
        await clientTransport.SendFrame2(expectedRequest, SerializeRequestPayload);
        (Frame actual, Memory<byte>? request) = await readRequestTask;

        Assert.That(actual, Is.EqualTo(expectedRequest));
        CollectionAssert.AreEqual(request?.ToArray(), expectedRequestPayload);

        (Memory<byte>, int) SerializeRequestPayload(Frame frame)
        {
            int frameSize = frame.CalculateSize();
            var owner = MemoryPool<byte>.Shared.Rent(frameSize + expectedRequestPayload.Length);
            Memory<byte> messageBytes = owner.Memory.Slice(0, frameSize + expectedRequestPayload.Length);
            frame.WriteTo(messageBytes.Span.Slice(0, frameSize));

            Memory<byte> payLoadBytes = messageBytes.Slice(frameSize);
            expectedRequestPayload.AsMemory()
                                  .CopyTo(payLoadBytes);

            return (messageBytes, expectedRequestPayload.Length);
        }
    }

    [Test]
    public async Task SendFrame3_Test()
    {
        using var channel = PipeChannel.CreateRandom();
        
        Fixture fixture = new();
        var expectedRequest = fixture.Create<Frame>();
        var expectedResponse = fixture.Create<Frame>();

        Random random = new();
        byte[] expectedRequestPayload = new byte[100];
        random.NextBytes(expectedRequestPayload);

        using var clientTransport = new NamedPipeTransportV2(channel.ClientStream);
        using var serverTransport = new NamedPipeTransportV2(channel.ServerStream);

        var readRequestTask = serverTransport.ReadFrame3();
        await clientTransport.SendFrame3(expectedRequest, SerializeRequestPayload);
        (Frame actual, Memory<byte>? request) = await readRequestTask;

        Assert.That(actual, Is.EqualTo(expectedRequest));
        CollectionAssert.AreEqual(request?.ToArray(), expectedRequestPayload);

        (Memory<byte> MsgBytes, int frameSize) SerializeRequestPayload(Frame frame)
        {
            int padding = NamedPipeTransportV2.FrameHeader.Size;
            int frameSize = frame.CalculateSize();
            //All
            var owner = MemoryPool<byte>.Shared.Rent(padding + frameSize + expectedRequestPayload.Length );
            Memory<byte> messageBytes = owner.Memory.Slice(0, padding +frameSize + expectedRequestPayload.Length);
            //#1 : will be set later in send method

            //#2 : frame
            Memory<byte> frameBytes = messageBytes.Slice(padding, frameSize);
            frame.WriteTo(frameBytes.Span);
            //#3 : payload
            Memory<byte> payLoadBytes = messageBytes.Slice(padding + frameSize);
            expectedRequestPayload.AsMemory()
                                  .CopyTo(payLoadBytes);

            return (messageBytes, frameSize);
        }
    }

    [Test]
    public async Task Frames_WithoutPayload_FullRoadTrip_Test()
    {
        //Arrange
        using var channel = PipeChannel.CreateRandom();
        Fixture fixture = new();
        var expectedRequest = fixture.Create<Frame>();
        var expectedResponse = fixture.Create<Frame>();

        using var clientTransport = new NamedPipeTransportV2(channel.ClientStream);
        using var serverTransport = new NamedPipeTransportV2(channel.ServerStream);

        //Act
        var readRequestTask = serverTransport.ReadFrame();
        await clientTransport.SendFrame(expectedRequest, null);
        (Frame actual, Memory<byte>? request) = await readRequestTask;
        //Assert
        Assert.That(actual, Is.EqualTo(expectedRequest));
        Assert.IsNull(request);
        //Act
        var readResponseTask = clientTransport.ReadFrame();
        await serverTransport.SendFrame(expectedResponse, null);
        (Frame? serverMessage, Memory<byte>? response) = await readResponseTask;
        //Assert
        Assert.That(serverMessage, Is.EqualTo(expectedResponse));
        Assert.IsNull(response);
    }


    [Test]
    public async Task Frames_Payload_FullRoadTrip_Test()
    {
        //Arrange
        using var channel = PipeChannel.CreateRandom();
        Random random = new();
        Fixture fixture = new();
        var expectedRequest = fixture.Create<Frame>();
        byte[] expectedRequestPayload = new byte[100];
        random.NextBytes(expectedRequestPayload);
        var expectedResponse = fixture.Create<Frame>();
        byte[] expectedResponsePayload = new byte[100];
        random.NextBytes(expectedResponsePayload);

        using var clientTransport = new NamedPipeTransportV2(channel.ClientStream);
        using var serverTransport = new NamedPipeTransportV2(channel.ServerStream);

        //Act
        var readRequestTask = serverTransport.ReadFrame();
        await clientTransport.SendFrame(expectedRequest, SerializeRequestPayload);
        (Frame actual, Memory<byte>? request) = await readRequestTask;
        //Assert
        Assert.That(actual, Is.EqualTo(expectedRequest));
        CollectionAssert.AreEqual(request?.ToArray(), expectedRequestPayload);
        //Act
        var readResponseTask = clientTransport.ReadFrame();
        await serverTransport.SendFrame(expectedResponse, SerializeResponsePayload);
        (Frame? serverMessage, Memory<byte>? response) = await readResponseTask;
        //Assert
        Assert.That(serverMessage, Is.EqualTo(expectedResponse));
        CollectionAssert.AreEqual(response?.ToArray(), expectedResponsePayload);


        void SerializeRequestPayload(MemoryStream stream) => stream.Write(expectedRequestPayload, 0, 100);
        void SerializeResponsePayload(MemoryStream stream) => stream.Write(expectedResponsePayload, 0, 100);
    }
}