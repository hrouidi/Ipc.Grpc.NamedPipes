#nullable enable
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.TransportProtocol;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class ServerConnection : IDisposable
    {
        private readonly IReadOnlyDictionary<string, Func<ServerConnection, ValueTask>> _methodHandlers;
        private readonly PayloadChannel<byte[]> _payloadChannel;
        private readonly NamedPipeServerStream _pipeStream;
        private readonly NamedPipeTransportV3 _transport;

        public CancellationTokenSource CancellationTokenSource { get; }

        public Deadline? Deadline { get; private set; }

        public Metadata? RequestHeaders { get; private set; }

        public ServerCallContext CallContext { get; }

        public bool IsCompleted { get; private set; }

        public Frame? UnaryRequestFrame { get; private set; }

        public ServerConnection(NamedPipeServerStream pipeStream, IReadOnlyDictionary<string, Func<ServerConnection, ValueTask>> methodHandlers)
        {
            CallContext = new NamedPipeCallContext(this);
            _pipeStream = pipeStream;
            _transport = new NamedPipeTransportV3(pipeStream);
            _methodHandlers = methodHandlers;
            _payloadChannel = new PayloadChannel<byte[]>();
            CancellationTokenSource = new CancellationTokenSource();
        }

        public void Dispose()
        {
            try
            {
                _pipeStream.Disconnect();
                _pipeStream.Dispose();
                _transport.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error : {ex.Message}");
            }
        }

        public async Task ListenMessagesAsync(CancellationToken shutdownToken)
        {
            while (IsCompleted == false && shutdownToken.IsCancellationRequested == false && _pipeStream.IsConnected)
            {
                Frame frame = await _transport.ReadFrame(shutdownToken).ConfigureAwait(false);
                switch (frame.Message.DataCase)
                {
                    case Message.DataOneofCase.Request:
                        if (frame.Message.Request.MethodType is Request.Types.MethodType.Unary or Request.Types.MethodType.ServerStreaming)
                        {
                            await HandleUnaryRequest(frame).ConfigureAwait(false);
                            return;
                        }

                        break;
                    case Message.DataOneofCase.RequestControl:
                        switch (frame.Message.RequestControl)
                        {
                            case Control.Cancel:
                                HandleCancel();
                                break;
                            case Control.StreamMessage:
                                break;
                            case Control.StreamMessageEnd:
                                break;
                            case Control.None:
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        break;
                    case Message.DataOneofCase.ResponseHeaders:
                    case Message.DataOneofCase.None:
                    case Message.DataOneofCase.Response:
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public ValueTask SendResponseHeaders(Metadata responseHeaders)
        {
            var token = CancellationTokenSource.Token;
            //ServerMessage message = TransportMessageBuilder.BuildResponseHeadersMessage(responseHeaders);
            //using MemoryStream ms = new();
            //message.WriteDelimitedTo(ms);
            //return SendOverPipeStream(ms, token);
            throw new NotImplementedException();
        }

        public IAsyncStreamReader<TRequest> GetRequestStreamReader<TRequest>(Marshaller<TRequest> requestMarshaller)
        {
            throw new NotImplementedException();
        }

        public IServerStreamWriter<TResponse> GetResponseStreamWriter<TResponse>(Marshaller<TResponse> responseMarshaller)
        {
            throw new NotImplementedException();
        }

        public async ValueTask Success<TResponse>(Marshaller<TResponse> marshaller = null, TResponse response = null)
            where TResponse : class
        {
            IsCompleted = true;
            (StatusCode status, string detail) = CallContext.Status.StatusCode switch
            {
                StatusCode.OK => (StatusCode.OK, ""),
                _ => (CallContext.Status.StatusCode, CallContext.Status.Detail)
            };

            await SendReply(marshaller, response, CallContext.ResponseTrailers, status, detail, CallContext.CancellationToken)
                .ConfigureAwait(false);
        }

        public async ValueTask Error<TResponse>(Exception ex)
            where TResponse : class
        {
            IsCompleted = true;
            (StatusCode status, string detail) = GetStatus();

            await SendReply<TResponse>(null, null, CallContext.ResponseTrailers, status, detail, CallContext.CancellationToken)
                .ConfigureAwait(false);

            (StatusCode status, string detail) GetStatus()
            {
                if (Deadline is { IsExpired: true })
                    return (StatusCode.DeadlineExceeded, "");

                if (CancellationTokenSource.IsCancellationRequested)
                    return (StatusCode.Cancelled, "");

                if (ex is RpcException rpcException)
                    return (rpcException.StatusCode, rpcException.Status.Detail);

                return (StatusCode.Unknown, "Exception was thrown by handler.");
            }
        }


        #region Message handlers

        private async ValueTask HandleUnaryRequest(Frame frame)
        {
            Request request = frame.Message.Request;
            UnaryRequestFrame = frame;
            Deadline = new Deadline(request.Deadline?.ToDateTime());
            RequestHeaders = MessageBuilder.ToMetadata(request.Headers.Metadata);
            //Task.Run(async () => await _methodHandlers[message.MethodFullName](this).ConfigureAwait(false));
            await _methodHandlers[request.MethodFullName](this).ConfigureAwait(false);
        }

        private ValueTask HandleRequestStreamPayload(byte[] payload) => _payloadChannel.Append(payload);

        private ValueTask HandleRequestStreamEnd()
        {
            return _payloadChannel.SetCompleted();
        }

        private void HandleCancel()
        {
            CancellationTokenSource.Cancel();
        }

        #endregion

        private ValueTask SendReply<TResponse>(Marshaller<TResponse>? marshaller, TResponse? response, Metadata trailers, StatusCode statusCode, string statusDetail, CancellationToken token)
            where TResponse : class
        {
            Message message = new()
            {
                Response = new Response
                {
                    Trailers = new()
                    {
                        StatusCode = (int)statusCode,
                        StatusDetail = statusDetail
                    }
                }
            };
            FrameInfo<TResponse> frameInfo = new(message, response, marshaller?.ContextualSerializer);
            return _transport.SendFrame(frameInfo, token);
        }
    }
}