#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal.Helpers;
using Ipc.Grpc.NamedPipes.Internal.Transport;
using Message = Ipc.Grpc.NamedPipes.Internal.Transport.Message;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class ServerConnection : IDisposable
    {
        private readonly IReadOnlyDictionary<string, Func<ServerConnection, ValueTask>> _methodHandlers;
        private readonly MessageChannel _messageChannel;
        private readonly NamedPipeServerStream _pipeStream;
        private readonly NamedPipeTransport _transport;

        private Message? _unaryRequestMessage;

        public CancellationTokenSource CancellationTokenSource { get; }

        public Deadline Deadline { get; private set; }

        public Metadata? RequestHeaders { get; private set; }

        public ServerCallContext CallContext { get; }

        public bool IsCompleted { get; private set; }

        public ServerConnection(NamedPipeServerStream pipeStream, IReadOnlyDictionary<string, Func<ServerConnection, ValueTask>> methodHandlers)
        {
            CallContext = new NamedPipeCallContext(this);
            _pipeStream = pipeStream;
            _transport = new NamedPipeTransport(pipeStream);
            _methodHandlers = methodHandlers;
            CancellationTokenSource = new CancellationTokenSource();
            Deadline = Deadline.None;
            _messageChannel = new MessageChannel(CancellationTokenSource.Token);
        }

        public void Dispose()
        {
            _transport.Dispose();
        }

        public async Task ListenMessagesAsync(CancellationToken shutdownToken)
        {
            while (IsCompleted == false && shutdownToken.IsCancellationRequested == false && _pipeStream.IsConnected)
            {
                Message message = await _transport.ReadFrame(shutdownToken).ConfigureAwait(false);

                if (message == Message.Eof) //gracefully end the task
                {
                    //Debug.Assert(IsCompleted, "invalid end");
                    //Debug.Assert(_pipeStream.IsMessageComplete, "Message is not complete");
                    //TODO: recycle this instance in PipePool instead of disposing it
                    _pipeStream.Dispose();
                    return;
                }

                switch (message.DataCase)
                {
                    case Message.DataOneofCase.Request:
                        _ = HandleRequestAsync(message);
                        break;
                    case Message.DataOneofCase.Cancel:
                        HandleCancel();
                        message.Dispose();
                        break;
                    case Message.DataOneofCase.Streaming:
                        _messageChannel.Append(message);
                        break;
                    case Message.DataOneofCase.StreamingEnd:
                        _messageChannel.SetCompleted();
                        message.Dispose();
                        break;
                    case Message.DataOneofCase.ResponseHeaders:
                    case Message.DataOneofCase.None:
                    case Message.DataOneofCase.Response:
                    default:
                        message.Dispose();
                        throw new ArgumentOutOfRangeException("vilaine erreur");
                }
            }
        }

        public ValueTask SendResponseHeaders(Metadata responseHeaders)
        {
            Message message = MessageBuilder.BuildResponseHeaders(responseHeaders);
            return _transport.SendFrame(message, CancellationTokenSource.Token);
        }

        public TRequest GetUnaryRequest<TRequest>(Marshaller<TRequest> requestMarshaller)
        {
            if (_unaryRequestMessage != null)
            {
                using Message? req = _unaryRequestMessage;
                return req.GetPayload(requestMarshaller.ContextualDeserializer);
            }

            throw new InvalidOperationException("No request payload was found");
        }

        public IAsyncStreamReader<TRequest> GetRequestStreamReader<TRequest>(Marshaller<TRequest> requestMarshaller)
        {
            return _messageChannel.GetAsyncStreamReader(Deadline, requestMarshaller.ContextualDeserializer);
        }

        public IServerStreamWriter<TResponse> GetResponseStreamWriter<TResponse>(Marshaller<TResponse> responseMarshaller) where TResponse : class
        {
            return new ResponseStreamWriter<TResponse>(_transport, CancellationTokenSource.Token, responseMarshaller.ContextualSerializer, () => IsCompleted);
        }

        public ValueTask Success<TResponse>(Marshaller<TResponse>? marshaller = null, TResponse? response = null) where TResponse : class
        {
            IsCompleted = true;
            (StatusCode status, string detail) = CallContext.Status.StatusCode switch
            {
                StatusCode.OK => (StatusCode.OK, ""),
                _ => (CallContext.Status.StatusCode, CallContext.Status.Detail)
            };

            Message message = MessageBuilder.BuildReply(CallContext.ResponseTrailers, status, detail);
            if (response != null && marshaller != null)
            {
                MessageInfo<TResponse> messageInfo = new(message, response, marshaller.ContextualSerializer);
                return _transport.SendFrame(messageInfo, CallContext.CancellationToken);
            }
            return _transport.SendFrame(message);
        }

        public ValueTask Error(Exception ex)
        {
            IsCompleted = true;
            (StatusCode status, string detail) = GetStatus();

            Message message = MessageBuilder.BuildReply(CallContext.ResponseTrailers, status, detail);
            return _transport.SendFrame(message, CallContext.CancellationToken);

            (StatusCode status, string detail) GetStatus()
            {
                if (Deadline is { IsExpired: true })
                    return (StatusCode.DeadlineExceeded, "");

                if (CancellationTokenSource.IsCancellationRequested)
                    return (StatusCode.Cancelled, "");

                if (ex is RpcException rpcException)
                    return (rpcException.StatusCode, rpcException.Status.Detail);

                return (StatusCode.Unknown, $"Exception was thrown by handler: {ex.Message}");
            }
        }


        #region Message handlers

        private async ValueTask HandleRequestAsync(Message message)//Should never throw
        {
            await Task.Yield();
            if (message.Request.MethodType is Request.Types.MethodType.Unary or Request.Types.MethodType.ServerStreaming)
                _unaryRequestMessage = message;

            Request request = message.Request;
            Deadline = new Deadline(request.Deadline?.ToDateTime());
            RequestHeaders = MessageBuilder.DecodeMetadata(request.Headers.Metadata);
            await _methodHandlers[request.MethodFullName](this).ConfigureAwait(false);
        }

        private void HandleCancel()
        {
            Console.WriteLine($"{nameof(ServerConnection)} Debug: Cancel current operation requested");
            CancellationTokenSource.Cancel();
            Console.WriteLine($"{nameof(ServerConnection)} Debug: Current operation cancelled");
        }

        #endregion

    }
}