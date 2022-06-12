using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal.Helpers;
using Ipc.Grpc.NamedPipes.TransportProtocol;

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
        private readonly NamedPipeTransportV3 _transport;
        private readonly CancellationTokenSource _disposeCts;
        private readonly CancellationTokenSource _combinedCts;
        private readonly MessageChannel<TResponse> _messageChannel;

        private readonly Task _sendTask;
        private Task _readTask;

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
            _transport = new NamedPipeTransportV3(_pipeStream);
            _responseHeadersTcs = new TaskCompletionSource<Metadata>(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeCts = new CancellationTokenSource();
            _combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_callOptions.CancellationToken, _deadline.Token, _disposeCts.Token);
            _messageChannel = new MessageChannel<TResponse>(method.ResponseMarshaller.ContextualDeserializer, _deadline, _combinedCts.Token);

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

        private void EnsureResponseHeadersSet(Metadata headers = null)
        {
            _responseHeadersTcs.TrySetResult(headers ?? new Metadata());
        }

        private async Task SafeSendRequestAsync()
        {
            CancellationToken combined = _combinedCts.Token;
            try
            {
                await _pipeStream.ConnectAsync(_connectionTimeout, combined)
                                 .ConfigureAwait(false);
                _pipeStream.ReadMode = PipeTransmissionMode.Message;

                _readTask = SafeReadAllMessagesAsync(_combinedCts.Token);

                Message message = MessageBuilder.BuildRequest(_method, _callOptions.Deadline, _callOptions.Headers);
                FrameInfo<TRequest> frameInfo = new(message, _request, _method.RequestMarshaller.ContextualSerializer);
                await _transport.SendFrame(frameInfo, combined)
                                .ConfigureAwait(false);

                Interlocked.Exchange(ref _isInServerSide, 1);
                _cancelRemoteRegistration = _callOptions.CancellationToken.Register(DisposeCall);
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _isInServerSide, 0);
                await _messageChannel.SetError(ex).ConfigureAwait(false);
                //throw ex switch
                //{
                //    TimeoutException or IOException => new RpcException(new Status(StatusCode.Unavailable, ex.Message)),
                //    OperationCanceledException when _deadline.IsExpired => new RpcException(new Status(StatusCode.DeadlineExceeded, "")),
                //    OperationCanceledException => new RpcException(Status.DefaultCancelled),
                //    _ => ex
                //};
            }
        }

        private async Task SafeReadAllMessagesAsync(CancellationToken token)
        {
            try
            {
                await ReadAllMessagesAsync(token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                await _messageChannel.SetError(e).ConfigureAwait(false); ;
            }
        }

        private async Task ReadAllMessagesAsync(CancellationToken token)
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

                        Response response = frame.Message.Response;

                        EnsureResponseHeadersSet();
                        _status = new Status((StatusCode)response.Trailers.StatusCode, response.Trailers.StatusDetail);
                        _responseTrailers = MessageBuilder.ToMetadata(response.Trailers.Metadata) ?? new Metadata();

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
                        var headerMetadata = MessageBuilder.ToMetadata(frame.Message.ResponseHeaders.Metadata);
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

        #region Unary Async

        //private TResponse _unaryResponse;

        public async Task<TResponse> GetResponseAsync()
        {
            try
            {
                TResponse ret = await _messageChannel.ReadAsync();
                return ret;
            }
            finally
            {
                Dispose();
            }
        }

        #endregion

        #region Server streaming Async

        public IAsyncStreamReader<TResponse> GetResponseStreamReader()
        {
            return _messageChannel;
        }

        //public async IAsyncStreamReader<TResponse> GetResponseStreamReader2()
        //{
        //    using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_callOptions.CancellationToken, _deadline.Token, _disposeCts.Token);
        //    CancellationToken combined = combinedCts.Token;
        //    try
        //    {
        //        await _pipeStream.ConnectAsync(_connectionTimeout, combined)
        //                         .ConfigureAwait(false);
        //        _pipeStream.ReadMode = PipeTransmissionMode.Message;

        //        Task readAllMessagesTask = ReadAllMessagesAsync(combined);

        //        Message message = MessageBuilder.BuildRequest(_method, _callOptions.Deadline, _callOptions.Headers);
        //        FrameInfo<TRequest> frameInfo = new(message, _request, _method.RequestMarshaller.ContextualSerializer);
        //        await _transport.SendFrame(frameInfo, combined)
        //                        .ConfigureAwait(false);

        //        Interlocked.Exchange(ref _isInServerSide, 1);
        //        using CancellationTokenRegistration cancellationRegistration = _callOptions.CancellationToken.Register(DisposeCall);
        //        await readAllMessagesTask.ConfigureAwait(false);
        //        return new PayloadStreamReader<TResponse>(_payloadChannel, _method.ResponseMarshaller.ContextualDeserializer,combined);
        //    }
        //    catch (Exception ex)
        //    {
        //        Interlocked.Exchange(ref _isInServerSide, 0);
        //        throw ex switch
        //        {
        //            TimeoutException or IOException => new RpcException(new Status(StatusCode.Unavailable, ex.Message)),
        //            OperationCanceledException when _deadline.IsExpired => new RpcException(new Status(StatusCode.DeadlineExceeded, "")),
        //            OperationCanceledException => new RpcException(Status.DefaultCancelled),
        //            _ => ex
        //        };
        //    }
        //    finally
        //    {
        //        Dispose();
        //    }
        //}

        #endregion

        #region Client streaming Async

        public IClientStreamWriter<TRequest> GetRequestStreamWriter()
        {
            throw new NotImplementedException();
            //return new RequestStreamWriter<TRequest>(Transport, _callOptions.CancellationToken, requestMarshaller);
        }

        #endregion

        #region Duplex streaming Async

        public IAsyncStreamReader<TResponse> GetResponseStreamReader2()
        {
            throw new NotImplementedException();
            //return new MessageStreamReader<TResponse>(_payloadChannel, responseMarshaller, _callOptions.CancellationToken, _deadline);
        }

        public IClientStreamWriter<TRequest> GetRequestStreamWriter2()
        {
            throw new NotImplementedException();
            //return new RequestStreamWriter<TRequest>(Transport, _callOptions.CancellationToken, requestMarshaller);
        }

        #endregion
    }



}