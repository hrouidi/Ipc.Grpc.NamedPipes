using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal.Helpers;
using Ipc.Grpc.NamedPipes.Internal.Transport;
using Message = Ipc.Grpc.NamedPipes.Internal.Transport.Message;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class ClientConnection<TRequest, TResponse> : IDisposable where TRequest : class where TResponse : class
    {
        private readonly int _connectionTimeout;

        private readonly NamedPipeClientStream _pipeStream;
        private readonly CallOptions _callOptions;
        private readonly Method<TRequest, TResponse> _method;
        private readonly TRequest _request;

        private readonly TaskCompletionSource<Metadata> _responseHeadersTcs;
        private readonly NamedPipeTransport _transport;
        private readonly MessageChannel _receptionChannel;
        private readonly Task<Exception> _sendTask;

        private readonly Deadline _deadline;
        private readonly CancellationTokenSource _disposeCts;
        private readonly CancellationTokenSource _combinedCts;
        private readonly CancellationToken _combinedToken;

        private CancellationTokenRegistration _cancelRemoteRegistration;
        private int _isInServerSide;// 1 : true,0 : false
        private Metadata _responseTrailers;
        private Status _status;

        public ClientConnection(NamedPipeClientStream pipeStream, CallOptions callOptions, int connectionTimeout, Method<TRequest, TResponse> method, TRequest request)
        {
            _pipeStream = pipeStream;
            _callOptions = callOptions;
            _connectionTimeout = connectionTimeout;
            _method = method;
            _request = request;

            _deadline = new Deadline(callOptions.Deadline);
            _transport = new NamedPipeTransport(_pipeStream);
            _responseHeadersTcs = new TaskCompletionSource<Metadata>(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeCts = new CancellationTokenSource();
            _combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_callOptions.CancellationToken, _deadline.Token, _disposeCts.Token);
            _combinedToken = _combinedCts.Token;
            _receptionChannel = new MessageChannel(_combinedToken);
            _sendTask = SendRequestAsync();
        }

        public void Dispose()
        {
            _pipeStream?.Dispose();
            _transport?.Dispose();
            _cancelRemoteRegistration.Dispose();
            _combinedCts.Dispose();
        }

        public Task<Metadata> ResponseHeadersAsync => _responseHeadersTcs.Task;

        public Status GetStatus() => _responseTrailers != null ? _status : throw new InvalidOperationException();

        public Metadata GetTrailers() => _responseTrailers ?? throw new InvalidOperationException();

        public async void DisposeCall()
        {
            try
            {
                _disposeCts.Cancel();
                if (Interlocked.CompareExchange(ref _isInServerSide, 0, 1) == 1)
                {
                    await _transport.SendFrame(MessageBuilder.CancelRequest)
                                    .ConfigureAwait(false);
                    Console.WriteLine($"{nameof(ClientConnection<TRequest, TResponse>)} : Cancel remote operation sent");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{nameof(ClientConnection<TRequest, TResponse>)} Cancel remote operation fail: {ex.Message}");
            }
            finally
            {
                Dispose();
            }
        }

        public async Task<TResponse> GetResponseAsync()
        {
            TResponse ret = await _receptionChannel.ReadAsync(_deadline, _method.ResponseMarshaller.ContextualDeserializer)
                                                   .ConfigureAwait(false);
            return ret;
        }

        public IAsyncStreamReader<TResponse> GetResponseStreamReader()
        {
            return _receptionChannel.GetAsyncStreamReader(_method.ResponseMarshaller.ContextualDeserializer, _deadline, _combinedToken);
        }

        public IClientStreamWriter<TRequest> GetRequestStreamWriter()
        {
            return new RequestStreamWriter<TRequest>(_transport, _method.RequestMarshaller.ContextualSerializer, _combinedToken, _sendTask);
        }

        private void EnsureResponseHeadersSet(Metadata headers = null)
        {
            _responseHeadersTcs.TrySetResult(headers ?? new Metadata());
        }

        private async Task<Exception> SendRequestAsync() //Should never throw
        {
            try
            {
                await _pipeStream.ConnectAsync(_connectionTimeout, _combinedToken)
                                 .ConfigureAwait(false);
                _pipeStream.ReadMode = PipeTransmissionMode.Message;

                _ = ReadAllMessagesAsync();

                Message message = MessageBuilder.BuildRequest(_method, _callOptions.Deadline, _callOptions.Headers);
                if (_request is null) // ClientStreaming or DuplexStreaming
                {
                    await _transport.SendFrame(message, _combinedToken)
                                    .ConfigureAwait(false);
                }
                else //Unary or ServerStreaming
                {
                    MessageInfo<TRequest> messageInfo = new(message, _request, _method.RequestMarshaller.ContextualSerializer);
                    await _transport.SendFrame(messageInfo, _combinedToken)
                                    .ConfigureAwait(false);
                }

                Interlocked.Exchange(ref _isInServerSide, 1);
                _cancelRemoteRegistration = _callOptions.CancellationToken.Register(DisposeCall);
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _isInServerSide, 0);
                _receptionChannel.SetError(ex);
                Dispose();
                return ex;
            }
            return null;
        }

        private async Task ReadAllMessagesAsync()//Should never throw
        {
            try
            {
                while (true)
                {
                    Message message = await _transport.ReadFrame(_combinedToken).ConfigureAwait(false);
                    if (message == Message.Eof)
                    {
                        Interlocked.Exchange(ref _isInServerSide, 0);
                        _receptionChannel.SetError(new RpcException(new Status(StatusCode.DataLoss, "Server close current connection")));
                        return;
                    }
                    switch (message.DataCase)
                    {
                        case Message.DataOneofCase.Response: //maybe it's a happy end: release pipe stream

                            Interlocked.Exchange(ref _isInServerSide, 0);// server has complete
                            Dispose();

                            Reply response = message.Response;

                            EnsureResponseHeadersSet();
                            _status = new Status((StatusCode)response.StatusCode, response.StatusDetail);
                            _responseTrailers = MessageBuilder.DecodeMetadata(response.Trailers) ?? new Metadata();

                            if (_status.StatusCode == StatusCode.OK)
                            {
                                if (_method.Type is MethodType.Unary or MethodType.ClientStreaming)
                                    _receptionChannel.Append(message);
                                _receptionChannel.SetCompleted();
                                return;
                            }
                            _receptionChannel.SetError(new RpcException(_status));
                            return;

                        case Message.DataOneofCase.ResponseHeaders:
                            Metadata headerMetadata = MessageBuilder.DecodeMetadata(message.ResponseHeaders.Metadata);
                            EnsureResponseHeadersSet(headerMetadata);
                            message.Dispose();
                            break;
                        case Message.DataOneofCase.Streaming:
                            _receptionChannel.Append(message);
                            break;
                        case Message.DataOneofCase.None:
                        case Message.DataOneofCase.Request:
                        case Message.DataOneofCase.StreamingEnd:
                        case Message.DataOneofCase.Cancel:
                        default:
                            Interlocked.Exchange(ref _isInServerSide, 0);
                            Dispose();
                            message.Dispose();
                            _receptionChannel.SetError(new RpcException(new Status(StatusCode.Unknown, $"Unexpected message received: {message.DataCase}")));
                            return;
                    }
                }
            }
            catch (Exception e)
            {
                _receptionChannel.SetError(e);
            }
        }
    }
}