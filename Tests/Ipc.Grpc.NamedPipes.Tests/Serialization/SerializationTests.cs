using System;
using System.IO.MemoryMappedFiles;
using System.Linq;
using AutoFixture;
using Google.Protobuf;
using NUnit.Framework;
using Ipc.Grpc.NamedPipes.Internal;
using Ipc.Grpc.NamedPipes.Internal.Transport;
using Message = Ipc.Grpc.NamedPipes.Internal.Transport.Message;

namespace Ipc.Grpc.NamedPipes.Tests.Serialization;

public class SerializationTests
{
    [Test]
    public void FullRoundTrip_Test()
    {
        Fixture fixture = new();
        var expectedRequest = fixture.Create<Message>();
        byte[] payloadBytes = { 1, 2, 3 };
        using SharedMemorySerializationContext serializationContext = new(expectedRequest);
        serializationContext.Complete(payloadBytes);

        using MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(serializationContext.MapName.ToString());
        MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(0, serializationContext.GlobalSize);
        Span<byte> bytes = accessor.GetSpan();
        NamedPipeTransport.FrameHeader header = NamedPipeTransport.FrameHeader.FromSpan(bytes.Slice(0, NamedPipeTransport.FrameHeader.Size));


        var deserializationContext = new SharedMemoryDeserializationContext(mmf, NamedPipeTransport.FrameHeader.Size + header.MessageSize, header.PayloadSize);
        byte[]? actual = deserializationContext.PayloadAsNewBuffer();
        Assert.That(payloadBytes.SequenceEqual(actual));
        Message ret = new(mmf, NamedPipeTransport.FrameHeader.Size + header.MessageSize, header.PayloadSize);
        ret.MergeFrom(bytes.Slice(NamedPipeTransport.FrameHeader.Size, header.MessageSize));
        Assert.That(ret, Is.EqualTo(expectedRequest));
    }
}