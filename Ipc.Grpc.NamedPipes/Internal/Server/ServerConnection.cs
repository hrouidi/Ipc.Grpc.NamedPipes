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

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class ServerConnection : IDisposable
    {
        private readonly IReadOnlyDictionary<string, Func<ServerConnection, ValueTask>> _methodHandlers;
        private readonly MessageChannel _requestStreamingChannel;
        private readonly NamedPipeServerStream _pipeStream;
        private readonly IPool<NamedPipeServerStream> _pipePool;
        private readonly NamedPipeTransport _transport;

        private readonly CancellationToken _serverShutdownToken;
        private readonly CancellationTokenSource _callContextCts;
        private readonly CancellationTokenSource _remoteClientCts;
        private readonly CancellationTokenSource _combinedCts;

        private Message? _unaryRequestMessage;

        private long _isCompleted; //1 :true | 0 : false
        private bool IsCompleted
        {
            get => Interlocked.Read(ref _isCompleted) == 1;
            set => Interlocked.Exchange(ref _isCompleted, value ? 1 : 0);
        }

        public CancellationToken CallContextCancellationToken => _callContextCts.Token;

        public Deadline Deadline { get; private set; }

        public Metadata? RequestHeaders { get; private set; }

        public ServerCallContext CallContext { get; }


        public ServerConnection(IPool<NamedPipeServerStream> pipePool, IReadOnlyDictionary<string, Func<ServerConnection, ValueTask>> methodHandlers, CancellationToken serverShutdownToken)
        {
            _pipePool = pipePool;
            _pipeStream = _pipePool.Rent();
            _methodHandlers = methodHandlers;
            _serverShutdownToken = serverShutdownToken;

            Deadline = Deadline.None;
            CallContext = new NamedPipeCallContext(this);
            _transport = new NamedPipeTransport(_pipeStream);
            _callContextCts = new CancellationTokenSource();
            _remoteClientCts = new CancellationTokenSource();
            _combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_serverShutdownToken, _callContextCts.Token, _remoteClientCts.Token);
            _requestStreamingChannel = new MessageChannel(_combinedCts.Token);
        }

        public void Dispose()
        {
            _transport.Dispose();
            _combinedCts.Dispose();
            _pipePool.Return(_pipeStream);
        }

        public void WaitForClientConnection()
        {
            _pipeStream.WaitForConnectionAsync(_serverShutdownToken).GetAwaiter().GetResult();
        }

        public async ValueTask HandleClientMessagesAsync()
        {
            var requestHandlerTask = Task.CompletedTask;
            while (IsCompleted == false && _combinedCts.IsCancellationRequested == false && _pipeStream.IsConnected)
            {
                Message message = await _transport.ReadFrame(_combinedCts.Token).ConfigureAwait(false);
                if (message.IsEof) //gracefully end the task
                    break;

                switch (message.DataCase)
                {
                    case Message.DataOneofCase.Request:
                        requestHandlerTask = Task.Run(() => HandleRequestAsync(message), _combinedCts.Token);
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
            await requestHandlerTask.ConfigureAwait(false);
        }

        public async ValueTask SendResponseHeaders(Metadata responseHeaders)
        {
            Message message = MessageBuilder.BuildResponseHeaders(responseHeaders);
            try
            {
                await _transport.SendFrame(message, _combinedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"{nameof(ServerConnection)}: Send Response headers cancelled by the remote client");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{nameof(ServerConnection)}: Send Response headers failed:{ex.Message}");
            }
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
            return new ResponseStreamWriter<TResponse>(_transport, _combinedCts.Token, responseMarshaller.ContextualSerializer, () => IsCompleted);
        }

        //Should never throw exception
        public async ValueTask Success<TResponse>(Marshaller<TResponse>? marshaller = null, TResponse? response = null) where TResponse : class
        {
            IsCompleted = true;
            (StatusCode status, string detail) = CallContext.Status.StatusCode switch
            {
                StatusCode.OK => (StatusCode.OK, string.Empty),
                _ => (CallContext.Status.StatusCode, CallContext.Status.Detail)
            };
            Message message = MessageBuilder.BuildReply(CallContext.ResponseTrailers, status, detail);
            if (response != null && marshaller != null)
            {
                MessageInfo<TResponse> messageInfo = new(message, response, marshaller.ContextualSerializer);
                await SendReplyStatus(messageInfo).ConfigureAwait(false);//not cancellable send
            }
            else
            {
                await SendReplyStatus(message).ConfigureAwait(false); //not cancellable send
            }
        }

        public async ValueTask Error(Exception ex)//Should never throw
        {
            IsCompleted = true;

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
                StatusCode status = GetStatus(out string detail);

                Message message = MessageBuilder.BuildReply(CallContext.ResponseTrailers, status, detail);
                await SendReplyStatus(message).ConfigureAwait(false); //not cancellable send
            }

            StatusCode GetStatus(out string detail)
            {
                detail = string.Empty;
                if (Deadline is { IsExpired: true })
                    return StatusCode.DeadlineExceeded;

                if (_callContextCts.IsCancellationRequested)
                    return StatusCode.Cancelled;

                if (ex is RpcException rpcException)
                {
                    detail = rpcException.Status.Detail;
                    return rpcException.StatusCode;
                }

                detail = $"Exception was thrown by handler: {ex.Message}";
                return StatusCode.Unknown;
            }
        }

        //Not Cancellable send
        private async ValueTask SendReplyStatus(Message message)
        {
            try
            {
                await _transport.SendFrame(message).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"{nameof(ServerConnection)}: Send {message.DataCase} cancelled by the remote client");
            }
            catch (Exception ex)
            {
                LogError(message, ex);
            }
        }
        //Not Cancellable send
        private async ValueTask SendReplyStatus<TResponse>(MessageInfo<TResponse> info) where TResponse : class
        {
            try
            {
                await _transport.SendFrame(info).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogError(info.Message, ex);
            }
        }

        private static void LogError(Message message, Exception? error)
        {
            Console.WriteLine($"{nameof(ServerConnection)}:[Error] Send reply error At {DateTime.UtcNow:hh:mm:ss.fff tt} : [Code: {message.Response.StatusCode}] [detail: {message.Response.StatusDetail}] [Error: {error?.Message}]");
        }


        #region Message handlers

        private async Task HandleRequestAsync(Message message)//Should never throw
        {
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