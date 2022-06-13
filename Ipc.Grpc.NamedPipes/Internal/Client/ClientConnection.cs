﻿using System;
using System.Diagnostics;
using System.IO;
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
        private readonly NamedPipeClientStream _pipeStream;
        private readonly CallOptions _callOptions;
        private readonly Deadline _deadline;
        private readonly int _connectionTimeout;
        private readonly Method<TRequest, TResponse> _method;
        private readonly TRequest _request;

        private readonly TaskCompletionSource<Metadata> _responseHeadersTcs;
        private readonly NamedPipeTransport _transport;
        private readonly CancellationTokenSource _disposeCts;
        private readonly CancellationTokenSource _combinedCts;
        private readonly MessageChannel _messageChannel;

        private readonly Task<Exception> _sendTask;

        CancellationTokenRegistration _cancelRemoteRegistration;
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
            _messageChannel = new MessageChannel(_combinedCts.Token);

            _sendTask = SafeSendRequestAsync();
        }

        public void Dispose()
        {
            _pipeStream?.Dispose();
            _transport?.Dispose();
            _cancelRemoteRegistration.Dispose();
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
                    Console.WriteLine("Debug : Cancel remote operation sent");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cancel remote operation fail: {ex.Message}");
            }
            finally
            {
                Dispose();
            }
        }

        public async Task<TResponse> GetResponseAsync()
        {
            try
            {
                TResponse ret = await _messageChannel.ReadAsync(_deadline, _method.ResponseMarshaller.ContextualDeserializer);
                return ret;
            }
            finally
            {
                Dispose();
            }
        }

        public IAsyncStreamReader<TResponse> GetResponseStreamReader()
        {
            return _messageChannel.GetAsyncStreamReader(_deadline, _method.ResponseMarshaller.ContextualDeserializer);
        }

        public IClientStreamWriter<TRequest> GetRequestStreamWriter()
        {
            return new RequestStreamWriter<TRequest>(_transport, _method.RequestMarshaller.ContextualSerializer, _combinedCts.Token, _sendTask);
        }



        private void EnsureResponseHeadersSet(Metadata headers = null)
        {
            _responseHeadersTcs.TrySetResult(headers ?? new Metadata());
        }

        private async Task<Exception> SafeSendRequestAsync()
        {
            CancellationToken combined = _combinedCts.Token;
            try
            {
                await _pipeStream.ConnectAsync(_connectionTimeout, combined)
                                 .ConfigureAwait(false);
                _pipeStream.ReadMode = PipeTransmissionMode.Message;

                _ = SafeReadAllMessagesAsync(_combinedCts.Token);

                Message message = MessageBuilder.BuildRequest(_method, _callOptions.Deadline, _callOptions.Headers);
                if (_request is null) // ClientStreaming or DuplexStreaming
                {
                    await _transport.SendFrame(message, combined)
                                    .ConfigureAwait(false);
                }
                else //Unary or ServerStreaming
                {
                    FrameInfo<TRequest> frameInfo = new(message, _request, _method.RequestMarshaller.ContextualSerializer);
                    await _transport.SendFrame(frameInfo, combined)
                                    .ConfigureAwait(false);
                }

                Interlocked.Exchange(ref _isInServerSide, 1);
                _cancelRemoteRegistration = _callOptions.CancellationToken.Register(DisposeCall);
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _isInServerSide, 0);
                await _messageChannel.SetError(ex).ConfigureAwait(false);
                //return ex;
                return MessageChannel.MapException(ex, _deadline);
            }
            return null;
        }

        private async Task SafeReadAllMessagesAsync(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    Frame frame = await _transport.ReadFrame(token).ConfigureAwait(false);
                    switch (frame.Message.DataCase)
                    {
                        case Message.DataOneofCase.Response: //maybe it's a happy end: release pipe stream
                                                             // server has complete
                            Interlocked.Exchange(ref _isInServerSide, 0);
                            _pipeStream.Dispose();

                            Reply response = frame.Message.Response;

                            EnsureResponseHeadersSet();
                            _status = new Status((StatusCode)response.StatusCode, response.StatusDetail);
                            _responseTrailers = MessageBuilder.DecodeMetadata(response.Trailers) ?? new Metadata();

                            if (_status.StatusCode == StatusCode.OK)
                            {
                                if (_method.Type is MethodType.Unary or MethodType.ClientStreaming)
                                    await _messageChannel.Append(frame).ConfigureAwait(false);
                                await _messageChannel.SetCompleted().ConfigureAwait(false);
                                return;
                            }
                            await _messageChannel.SetError(new RpcException(_status)).ConfigureAwait(false);
                            return;

                        case Message.DataOneofCase.ResponseHeaders:
                            var headerMetadata = MessageBuilder.DecodeMetadata(frame.Message.ResponseHeaders.Metadata);
                            EnsureResponseHeadersSet(headerMetadata);
                            break;
                        case Message.DataOneofCase.RequestControl:
                            switch (frame.Message.RequestControl)
                            {
                                case Control.StreamMessage:
                                    await _messageChannel.Append(frame).ConfigureAwait(false);
                                    break;
                                case Control.None:
                                case Control.Cancel:
                                case Control.StreamMessageEnd:
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                            break;
                        case Message.DataOneofCase.None:
                        case Message.DataOneofCase.Request:
                        default:
                            Interlocked.Exchange(ref _isInServerSide, 0);
                            _pipeStream.Dispose();
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
            catch (Exception e)
            {
                await _messageChannel.SetError(e).ConfigureAwait(false);
            }
        }
    }
}