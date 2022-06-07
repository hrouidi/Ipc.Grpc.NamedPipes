#nullable enable
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal.Helpers;
using Ipc.Grpc.NamedPipes.Protocol;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class NamedPipeTransportV2
    {
        private const int _messageSize = 4;
        private readonly byte[] _messageSizeBuffer = new byte[_messageSize];
        private readonly PipeStream _pipeStream;

        public NamedPipeTransportV2(PipeStream pipeStream) => _pipeStream = pipeStream;


        #region new Client

        private async ValueTask<(ServerMessage, MemoryStream? rep)> ReadServerFrame(CancellationToken token = default)
        {
            MemoryStream? packet = await ReadOverPipeStream(token).ConfigureAwait(false);
            ServerMessage message = ServerMessage.Parser.ParseDelimitedFrom(packet);
            return (message, packet);
        }

        public ValueTask SendClientFrame(ClientMessage message, Action<MemoryStream>? requestSerializer, CancellationToken token)
        {
            MemoryStream ms = new();
            message.WriteDelimitedTo(ms);
            requestSerializer?.Invoke(ms);
            return SendOverPipeStream(ms, token);
        }

        #endregion

        #region new Server

        private async ValueTask<(ClientMessage, MemoryStream? req)> ReadClientFrame(CancellationToken token = default)
        {
            MemoryStream? packet = await ReadOverPipeStream(token).ConfigureAwait(false);
            ClientMessage message = ClientMessage.Parser.ParseDelimitedFrom(packet);
            return (message, packet);
        }

        public ValueTask SendServerFrame(ServerMessage message, Action<MemoryStream>? requestSerializer, CancellationToken token)
        {
            MemoryStream ms = new();
            message.WriteDelimitedTo(ms);
            requestSerializer?.Invoke(ms);
            return SendOverPipeStream(ms, token);
        }


        #endregion


        //TODO : make this allocation free
        private async ValueTask<MemoryStream> ReadOverPipeStream(CancellationToken token = default)
        {
            int readBytes = await _pipeStream.ReadAsync(_messageSizeBuffer, 0, _messageSize, token).ConfigureAwait(false);
            int messageSize = DecodeSize(_messageSizeBuffer);

            IMemoryOwner<byte> manager = MemoryPool<byte>.Shared.Rent(messageSize);
            Memory<byte> buffer = manager.Memory.Slice(0, messageSize);

            readBytes = await _pipeStream.ReadAsync(buffer, token)
                                         .ConfigureAwait(false);


            var packet = new MemoryStream(buffer.Length);
            await packet.WriteAsync(buffer, token).ConfigureAwait(false);
            packet.Position = 0;
            return packet;
        }

        //TODO : make this allocation free
        private async ValueTask SendOverPipeStream(MemoryStream frame, CancellationToken token)
        {
            using var manager = MemoryPool<byte>.Shared.Rent(sizeof(int));
            Memory<byte> bytes = manager.Memory.Slice(0, sizeof(int));
            EncodeSize(bytes.Span, (int)frame.Length);
            await _pipeStream.WriteAsync(bytes, token).ConfigureAwait(false);
            frame.WriteTo(_pipeStream);
            frame.Dispose();
        }

        private static void EncodeSize(in Span<byte> destination, int size)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, size);
        }
        private static int DecodeSize(in ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadInt32LittleEndian(bytes);

    }
}