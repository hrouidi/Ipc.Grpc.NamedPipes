using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal.Helpers;
using Ipc.Grpc.NamedPipes.Protocol;
using Ipc.Grpc.NamedPipes.TransportProtocol;
using Headers = Ipc.Grpc.NamedPipes.Protocol.Headers;
using Response = Ipc.Grpc.NamedPipes.Protocol.Response;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class NamedPipeTransport : IDisposable
    {
        private const int _messageSize = 4;
        private readonly byte[] _messageSizeBuffer = new byte[_messageSize];
        private readonly PipeStream _pipeStream;

        private readonly byte[] _frameHeaderBytes;

        public NamedPipeTransport(PipeStream pipeStream)
        {
            _pipeStream = pipeStream;
            _frameHeaderBytes = ArrayPool<byte>.Shared.Rent(NamedPipeTransportV2.FrameHeader.Size);
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_frameHeaderBytes);
        }

        private async Task<MemoryStream> ReadPacketFromPipe(CancellationToken token = default)
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

        private static async ValueTask<byte[]> TryReadPayloadBytesAsync(Stream packet, int size)
        {
            if (size == 0)
                return Array.Empty<byte>();
            if (size > 0)
            {
                var payload = new byte[size];
                int count = await packet.ReadAsync(payload, 0, payload.Length).ConfigureAwait(false);
                return payload;
            }
            return null; // in server response case only
        }

        #region Client

        //Client AsyncUnaryCall  

        public async Task ReadServerMessages(IServerMessageHandler messageHandler)
        {
            using MemoryStream packet = await ReadPacketFromPipe().ConfigureAwait(false);
            while (packet.Position < packet.Length)
            {
                ServerMessage message = ServerMessage.Parser.ParseDelimitedFrom(packet);
                switch (message.DataCase)
                {
                    case ServerMessage.DataOneofCase.ResponseHeaders:
                        messageHandler.HandleResponseHeaders(message.ResponseHeaders);
                        break;
                    case ServerMessage.DataOneofCase.StreamPayloadInfo:
                        byte[] streamPayload = await TryReadPayloadBytesAsync(packet, message.StreamPayloadInfo.PayloadSize).ConfigureAwait(false);
                        await messageHandler.HandleResponseStreamPayload(streamPayload).ConfigureAwait(false); ;
                        break;
                    case ServerMessage.DataOneofCase.Response:
                        byte[] payload = await TryReadPayloadBytesAsync(packet, message.Response.PayloadSize).ConfigureAwait(false);
                        await messageHandler.HandleResponseAsync(message.Response, payload).ConfigureAwait(false); ;
                        break;
                }
            }
        }

        //TODO : make this allocation free
        public ValueTask SendUnaryRequest<TRequest, TResponse>(Method<TRequest, TResponse> method, TRequest request, DateTime? deadline, Metadata headers, CancellationToken token)
        {
            ClientMessage message = TransportMessageBuilder.BuildRequest2(method, request, deadline, headers);
            MemoryStream ms = new();
            message.WriteDelimitedTo(ms);
            if (request != null)
            {
                SerializationHelpers.Serialize(ms, method.RequestMarshaller, request);
            }

            return SendOverPipeStream00(ms, token);
        }

        public ValueTask SendUnaryRequest2<TRequest, TResponse>(Method<TRequest, TResponse> method, TRequest request, DateTime? deadline, Metadata headers, CancellationToken token)
        {
            

            Frame frame = new()
            {
                Request = new TransportProtocol.Request
                {
                    MethodFullName = method.FullName,
                    MethodType = (TransportProtocol.Request.Types.MethodType)method.Type,
                    Deadline = deadline != null ? Timestamp.FromDateTime(deadline.Value) : null,
                    Headers = new(),//TODO: 
                }
            };

            // Transport.SendFrame packet
            //if (request != null)
            //{
            using var serializationContext = new MemorySerializationContext(frame);
            method.RequestMarshaller.ContextualSerializer(request, serializationContext);
            return SendOverPipeStream2(serializationContext.Bytes, serializationContext.FrameSize, token);
            //}

            //return SendOverPipeStream00(ms, token);

        }

        public void SendRequest<TRequest, TResponse>(Method<TRequest, TResponse> method, TRequest request, DateTime? deadline, Metadata headers)
        {
            (ClientMessage message, byte[] payload) = TransportMessageBuilder.BuildRequest(method, request, deadline, headers);
            using MemoryStream ms = new();
            message.WriteDelimitedTo(ms);
            if (message.Request.PayloadSize > 0)
                ms.Write(payload, 0, payload.Length);
            ms.WriteTo(_pipeStream);
        }

        public void SendStreamRequestPayload<TRequest>(Marshaller<TRequest> marshaller, TRequest request)
        {
            (ClientMessage message, byte[] payload) = TransportMessageBuilder.BuildStreamPayload(marshaller, request);
            using MemoryStream ms = new();
            message.WriteDelimitedTo(ms);
            ms.Write(payload, 0, payload.Length);
            ms.WriteTo(_pipeStream);
        }

        public void SendRequestPayloadStreamEnd()
        {
            using MemoryStream ms = new();
            TransportMessageBuilder.StreamEnd.WriteDelimitedTo(ms);
            ms.WriteTo(_pipeStream);
        }

        public void SendCancelRequest()
        {
            using MemoryStream ms = new();
            TransportMessageBuilder.CancelRequest.WriteDelimitedTo(ms);
            ms.WriteTo(_pipeStream);
        }

        #endregion

        //TODO : make this allocation free

        private async ValueTask SendOverPipeStream2(Memory<byte> messageBytes, int frameSize, CancellationToken token)
        {
            Debug.Assert(messageBytes.Length >= frameSize + NamedPipeTransportV2.FrameHeader.Size);
            //Header to bytes
            //var header = new FrameHeader(messageBytes.Length, frameSize);
            NamedPipeTransportV2.FrameHeader.ToSpan3(messageBytes.Span.Slice(0, NamedPipeTransportV2.FrameHeader.Size), messageBytes.Length, frameSize);

            //#1 : Write header bytes (always fixed size = 8 bytes) [total size of Frame + payload,size of Frame ] + Write Frame message + payload if any
            await _pipeStream.WriteAsync(messageBytes, token).ConfigureAwait(false);
            //#3 : release messageBytes memory
            //TODO:
        }

        private async ValueTask SendOverPipeStream00(MemoryStream frame, CancellationToken token)
        {
            using var manager = MemoryPool<byte>.Shared.Rent(sizeof(int));
            Memory<byte> bytes = manager.Memory.Slice(0, sizeof(int));
            EncodeSize(bytes.Span, (int)frame.Length);
            await _pipeStream.WriteAsync(bytes, token).ConfigureAwait(false);
            frame.WriteTo(_pipeStream);
            frame.Dispose();
        }

        private async ValueTask SendOverPipeStream(MemoryStream frame, CancellationToken token)
        {
            byte[] bytes = new byte[sizeof(int)];
            EncodeSize(bytes, (int)frame.Length);
            //await _pipeStream.WriteAsync(bytes, token).ConfigureAwait(false);
            await frame.WriteAsync(bytes, token).ConfigureAwait(false);
            frame.WriteTo(_pipeStream);
            frame.Dispose();
        }

        #region Server

        public async ValueTask<(Frame, Memory<byte>? payloadBytes, IMemoryOwner<byte> owner)> ReadFrame3(CancellationToken token = default)
        {

            int readBytes = await _pipeStream.ReadAsync(_frameHeaderBytes, 0, NamedPipeTransportV2.FrameHeader.Size, token)
                                             .ConfigureAwait(false);
            Debug.Assert(readBytes == NamedPipeTransportV2.FrameHeader.Size, "Client does not speak my dialect :/");

            NamedPipeTransportV2.FrameHeader header = NamedPipeTransportV2.FrameHeader.FromSpan(_frameHeaderBytes.AsSpan().Slice(0, NamedPipeTransportV2.FrameHeader.Size));

            IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(header.TotalSize);
            Memory<byte> framePlusPayloadBytes = owner.Memory.Slice(0, header.TotalSize - NamedPipeTransportV2.FrameHeader.Size);

            readBytes = await _pipeStream.ReadAsync(framePlusPayloadBytes, token)
                                         .ConfigureAwait(false);

            Debug.Assert(readBytes == header.TotalSize - NamedPipeTransportV2.FrameHeader.Size, "Client is a layer !");
            Debug.Assert(_pipeStream.IsMessageComplete, "Unexpected message :too long!");

            //MemoryStream ms =new()
            Frame? message = Frame.Parser.ParseFrom(framePlusPayloadBytes.Span.Slice(0, header.FrameSize));
            if (header.PayloadSize == 0)
            {
                owner.Dispose();
                return (message, null, null);
            }
            var payloadBytes = framePlusPayloadBytes.Slice(header.FrameSize);
            return (message, payloadBytes, owner);
        }

        public async Task ReadClientMessages(IClientMessageHandler messageHandler)
        {
            (Frame frame, var payloadBytes, var owner) = await ReadFrame3().ConfigureAwait(false);
            if (frame.DataCase == Frame.DataOneofCase.Request)
            {
                await messageHandler.HandleUnaryRequest2(frame.Request, payloadBytes.Value, owner).ConfigureAwait(false);
                return;
            }

            throw new InvalidOperationException();
            //////////////////////////////////////////////
            //MemoryStream packet = await ReadPacketFromPipe().ConfigureAwait(false);
            //while (packet.Position < packet.Length)
            //{
            //    ClientMessage message = ClientMessage.Parser.ParseDelimitedFrom(packet);
            //    switch (message.DataCase)
            //    {
            //        case ClientMessage.DataOneofCase.Request:
            //            if (message.Request.MethodType == Request.Types.MethodType.Unary)
            //            {
            //                await messageHandler.HandleUnaryRequest(message.Request, packet).ConfigureAwait(false);
            //                return;
            //            }
            //            else
            //            {
            //                byte[] payload = await TryReadPayloadBytesAsync(packet, message.Request.PayloadSize).ConfigureAwait(false);
            //                messageHandler.HandleRequest(message.Request, payload);
            //                packet.Dispose();
            //            }
            //            break;
            //        case ClientMessage.DataOneofCase.StreamPayloadInfo:
            //            byte[] streamPayload = await TryReadPayloadBytesAsync(packet, message.StreamPayloadInfo.PayloadSize).ConfigureAwait(false);
            //            await messageHandler.HandleRequestStreamPayload(streamPayload).ConfigureAwait(false);
            //            packet.Dispose();
            //            break;
            //        case ClientMessage.DataOneofCase.RequestControl:
            //            switch (message.RequestControl)
            //            {
            //                case RequestControl.Cancel:
            //                    messageHandler.HandleCancel();
            //                    break;
            //                case RequestControl.StreamEnd:
            //                    await messageHandler.HandleRequestStreamEnd().ConfigureAwait(false); ;
            //                    break;
            //            }
            //            packet.Dispose();
            //            break;
            //    }
            //}
        }

        //TODO : make this allocation free
        public ValueTask SendUnaryResponse<TResponse>(Marshaller<TResponse> marshaller, TResponse response, Metadata trailers, StatusCode statusCode, string statusDetail, CancellationToken token)
        {
            Frame frame = new()
            {
                Response = new TransportProtocol.Response
                {
                    Trailers = new()
                    {
                        StatusCode = (int)statusCode,
                        StatusDetail = statusDetail
                    }
                }
            };

            using var serializationContext = new MemorySerializationContext(frame);
            marshaller.ContextualSerializer(response, serializationContext);

            return SendOverPipeStream2(serializationContext.Bytes, serializationContext.FrameSize, token);
        }

        public ValueTask SendUnaryResponse(Metadata trailers, StatusCode statusCode, string statusDetail, CancellationToken token)
        {
            ServerMessage message = TransportMessageBuilder.BuildUnaryResponse(trailers, statusCode, statusDetail);
            MemoryStream ms = new();
            message.WriteDelimitedTo(ms);
            return SendOverPipeStream(ms, token);
        }

        public void SendResponse(byte[] response, Metadata trailers, StatusCode statusCode, string statusDetail)
        {
            ServerMessage message = TransportMessageBuilder.BuildResponse(response?.Length >= 0 ? response.Length : -1, trailers, statusCode, statusDetail);
            using MemoryStream ms = new();
            message.WriteDelimitedTo(ms);
            if (response != null)
                ms.Write(response, 0, response.Length);
            ms.WriteTo(_pipeStream);
        }

        //TODO : fix SendResponseHeaders
        public ValueTask SendResponseHeaders(Metadata responseHeaders, CancellationToken token)
        {
            ServerMessage message = TransportMessageBuilder.BuildResponseHeadersMessage(responseHeaders);
            using MemoryStream ms = new();
            message.WriteDelimitedTo(ms);
            return SendOverPipeStream(ms, token);
        }

        public void SendStreamResponsePayload<TResponse>(Marshaller<TResponse> marshaller, TResponse response)
        {
            (ServerMessage message, byte[] payload) = TransportMessageBuilder.BuildResponseStreamPayload(marshaller, response);
            using MemoryStream ms = new();
            message.WriteDelimitedTo(ms);
            ms.Write(payload, 0, payload.Length);
            ms.WriteTo(_pipeStream);
        }

        #endregion

        private static void EncodeSize(in Span<byte> destination, int size)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, size);
        }
        private static int DecodeSize(in ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadInt32LittleEndian(bytes);

    }

    public sealed class ServerResponse
    {
        public static readonly ServerResponse Empty = new();
        public ServerMessage.DataOneofCase Type { get; }
        public Headers Headers { get; }
        public MemoryStream Payload { get; }
        public Response Response { get; }

        private ServerResponse() { }

        public ServerResponse(Headers headers)
        {
            Headers = headers;
            Type = ServerMessage.DataOneofCase.ResponseHeaders;
        }

        public ServerResponse(MemoryStream payload)
        {
            Payload = payload;
            Type = ServerMessage.DataOneofCase.StreamPayloadInfo;
        }
        public ServerResponse(Response response, MemoryStream payload)
        {
            Response = response;
            Payload = payload;
            Type = ServerMessage.DataOneofCase.Response;
        }
    }
}