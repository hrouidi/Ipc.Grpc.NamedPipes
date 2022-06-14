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
        private readonly MessageChannel _requestStreamingChannel;
        private readonly NamedPipeServerStream _pipeStream;
        private readonly NamedPipeTransport _transport;

        private readonly CancellationToken _serverShutdownToken;
        private readonly CancellationTokenSource _callContextCts;
        private readonly CancellationTokenSource _remoteClientCts;
        private readonly CancellationTokenSource _combinedCts;

        private Message? _unaryRequestMessage;
        private bool _isCompleted;//TODO : make this thread safe

        public CancellationToken CallContextCancellationToken => _callContextCts.Token;

        public Deadline Deadline { get; private set; }

        public Metadata? RequestHeaders { get; private set; }

        public ServerCallContext CallContext { get; }


        public ServerConnection(NamedPipeServerStream pipeStream, IReadOnlyDictionary<string, Func<ServerConnection, ValueTask>> methodHandlers, CancellationToken serverShutdownToken)
        {
            _pipeStream = pipeStream;
            _methodHandlers = methodHandlers;
            _serverShutdownToken = serverShutdownToken;

            Deadline = Deadline.None;
            CallContext = new NamedPipeCallContext(this);
            _transport = new NamedPipeTransport(pipeStream);
            _callContextCts = new CancellationTokenSource();
            _remoteClientCts = new CancellationTokenSource();
            _combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_serverShutdownToken, _callContextCts.Token, _remoteClientCts.Token);
            _requestStreamingChannel = new MessageChannel(_combinedCts.Token);
        }

        public void Dispose()
        {
            _transport.Dispose();
            _combinedCts.Dispose();
        }

        public async Task ListenMessagesAsync()
        {
            while (_isCompleted == false && _combinedCts.IsCancellationRequested == false && _pipeStream.IsConnected)
            {
                Message message = await _transport.ReadFrame(_combinedCts.Token).ConfigureAwait(false);

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
                        HandleRemoteCancel();
                        message.Dispose();
                        break;
                    case Message.DataOneofCase.Streaming:
                        _requestStreamingChannel.Append(message);
                        break;
                    case Message.DataOneofCase.StreamingEnd:
                        _requestStreamingChannel.SetCompleted();
                        message.Dispose();
                        break;
                    case Message.DataOneofCase.ResponseHeaders:
                    case Message.DataOneofCase.None:
                    case Message.DataOneofCase.Response:
                    default://Ignore others messages
                        message.Dispose();
                        break;
                }
            }
        }

        public ValueTask SendResponseHeaders(Metadata responseHeaders)
        {
            Message message = MessageBuilder.BuildResponseHeaders(responseHeaders);
            return Send(message);
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
            return _requestStreamingChannel.GetAsyncStreamReader(requestMarshaller.ContextualDeserializer, Deadline, _combinedCts.Token);
        }

        public IServerStreamWriter<TResponse> GetResponseStreamWriter<TResponse>(Marshaller<TResponse> responseMarshaller) where TResponse : class
        {
            return new ResponseStreamWriter<TResponse>(_transport, _combinedCts.Token, responseMarshaller.ContextualSerializer, () => _isCompleted);
        }

        //Should never throw exception
        public async ValueTask Success<TResponse>(Marshaller<TResponse>? marshaller = null, TResponse? response = null) where TResponse : class
        {
            _isCompleted = true;
            (StatusCode status, string detail) = CallContext.Status.StatusCode switch
            {
                StatusCode.OK => (StatusCode.OK, string.Empty),
                _ => (CallContext.Status.StatusCode, CallContext.Status.Detail)
            };
            Message message = MessageBuilder.BuildReply(CallContext.ResponseTrailers, status, detail);
            if (response != null && marshaller != null)
            {
                MessageInfo<TResponse> messageInfo = new(message, response, marshaller.ContextualSerializer);
                await Send(messageInfo).ConfigureAwait(false);
            }
            await Send(message).ConfigureAwait(false);
        }

        public async ValueTask Error(Exception ex)//Should never throw
        {
            _isCompleted = true;

            if (_serverShutdownToken.IsCancellationRequested) // don't notify remote client
            {
                Console.WriteLine($"{nameof(ServerConnection)}: Current request cancelled, server shutdown:{ex.Message}");
            }
            else if (_remoteClientCts.IsCancellationRequested)// don't notify remote client
            {
                Console.WriteLine($"{nameof(ServerConnection)}: Current request cancelled by the remote client");
            }
            else
            {
                (StatusCode status, string detail) = GetStatus();

                Message message = MessageBuilder.BuildReply(CallContext.ResponseTrailers, status, detail);
                await Send(message).ConfigureAwait(false);
            }

            (StatusCode status, string detail) GetStatus()
            {
                if (Deadline is { IsExpired: true })
                    return (StatusCode.DeadlineExceeded, "");

                if (_callContextCts.IsCancellationRequested)
                    return (StatusCode.Cancelled, "");

                if (ex is RpcException rpcException)
                    return (rpcException.StatusCode, rpcException.Status.Detail);

                return (StatusCode.Unknown, $"Exception was thrown by handler: {ex.Message}");
            }
        }

        //Cancellable send
        private async ValueTask Send(Message message)
        {
            try
            {
                await _transport.SendFrame(message, _remoteClientCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"{nameof(ServerConnection)}: Send {message.DataCase} cancelled by the remote client");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{nameof(ServerConnection)}:[Error] Send {message.DataCase} failed :{ex.Message}");
            }
        }
        //Cancellable send
        private async ValueTask Send<TResponse>(MessageInfo<TResponse> info) where TResponse : class
        {
            try
            {
                await _transport.SendFrame(info, _remoteClientCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"{nameof(ServerConnection)}: Send {info.Message.DataCase} cancelled by the remote client");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{nameof(ServerConnection)}:[Error] Send {info.Message.DataCase} failed :{ex.Message}");
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

        private void HandleRemoteCancel()
        {
            _remoteClientCts.Cancel();
            Console.WriteLine($"{nameof(ServerConnection)}: Current operation cancelled by remote client");
        }

        #endregion

    }
}