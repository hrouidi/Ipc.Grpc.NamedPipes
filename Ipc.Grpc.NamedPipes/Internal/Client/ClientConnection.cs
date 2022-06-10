﻿using System;
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

        private CancellationTokenRegistration _cancelReg;
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
        }

        public void Dispose()
        {
            _pipeStream?.Dispose();
            _transport?.Dispose();
            _cancelReg.Dispose();
        }

        public Task<Metadata> ResponseHeadersAsync => _responseHeadersTcs.Task;

        public Status GetStatus() => _responseTrailers != null ? _status : throw new InvalidOperationException();

        public Metadata GetTrailers() => _responseTrailers ?? throw new InvalidOperationException();

        public async void DisposeCall()
        {
            try
            {
                _pipeStream.Dispose();
                _cancelReg.Dispose();
                await _transport.SendFrame(MessageBuilder.CancelRequest)
                                .ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Assume the connection is already terminated
            }
        }

        private void EnsureResponseHeadersSet(Metadata headers = null)
        {
            _responseHeadersTcs.TrySetResult(headers ?? new Metadata());
        }

        #region Unary Async

        public async Task<TResponse> GetResponseAsync()
        {
            var combined = CancellationTokenSource.CreateLinkedTokenSource(_callOptions.CancellationToken, _deadline.Token);
            try
            {
                await _pipeStream.ConnectAsync(_connectionTimeout, combined.Token)
                                 .ConfigureAwait(false);

                _pipeStream.ReadMode = PipeTransmissionMode.Message;
                Task<TResponse> readTask = ReadResponsePayload(combined.Token);

                Message message = MessageBuilder.BuildRequest(_method, _callOptions.Deadline, _callOptions.Headers);
                FrameInfo<TRequest> frameInfo = new(message, _request, _method.RequestMarshaller.ContextualSerializer);
                await _transport.SendFrame(frameInfo, combined.Token)
                                .ConfigureAwait(false);

                //_cancelReg = _callOptions.CancellationToken.Register(DisposeCall);
                TResponse ret = await readTask.ConfigureAwait(false);
                return ret;
            }
            catch (Exception ex)
            {
                if (ex is TimeoutException  or IOException )
                    throw new RpcException(new Status(StatusCode.Unavailable, ex.Message));

                if (ex is OperationCanceledException)
                {
                    if (_deadline.IsExpired)
                        throw new RpcException(new Status(StatusCode.DeadlineExceeded, ""));
                    throw new RpcException(Status.DefaultCancelled);
                }

                throw ex;
            }
            finally
            {
                Dispose();
            }
        }

        private async Task<TResponse> ReadResponsePayload(CancellationToken token)
        {
            while (_pipeStream.IsConnected && token.IsCancellationRequested == false)
            {
                using Frame frame = await _transport.ReadFrame(token).ConfigureAwait(false);
                switch (frame.Message.DataCase)
                {
                    case Message.DataOneofCase.Response:
                        //may be it's a happy end: release pipe stream
                        _pipeStream.Dispose();

                        Response response = frame.Message.Response;

                        EnsureResponseHeadersSet();
                        _status = new Status((StatusCode)response.Trailers.StatusCode, response.Trailers.StatusDetail);
                        _responseTrailers = MessageBuilder.ToMetadata(response.Trailers.Metadata) ?? new Metadata();

                        if (_status.StatusCode == StatusCode.OK)
                        {
                            TResponse ret = frame.GetPayload(_method.ResponseMarshaller.ContextualDeserializer);
                            return ret;
                        }
                        throw new RpcException(_status);

                    case Message.DataOneofCase.ResponseHeaders:
                        var headerMetadata = MessageBuilder.ToMetadata(frame.Message.ResponseHeaders.Metadata);
                        EnsureResponseHeadersSet(headerMetadata);
                        break;
                    default:
                        _pipeStream.Dispose();
                        throw new ArgumentOutOfRangeException();
                }
            }

            throw new InvalidProgramException();
        }

        #endregion

        #region Server streaming Async

        public IAsyncStreamReader<TResponse> GetResponseStreamReader()
        {
            throw new NotImplementedException();
            //return new MessageStreamReader<TResponse>(_payloadChannel, responseMarshaller, _callOptions.CancellationToken, _deadline);
        }

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