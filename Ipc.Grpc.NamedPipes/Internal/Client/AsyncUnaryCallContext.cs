using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.TransportProtocol;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class AsyncUnaryCallContext<TRequest, TResponse> where TRequest : class where TResponse : class
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

        public AsyncUnaryCallContext(NamedPipeClientStream pipeStream, CallOptions callOptions, int connectionTimeout, Method<TRequest, TResponse> method, TRequest request)
        {
            _pipeStream = pipeStream;
            _callOptions = callOptions;
            _transport = new NamedPipeTransportV3(_pipeStream);
            _responseHeadersTcs = new TaskCompletionSource<Metadata>(TaskCreationOptions.RunContinuationsAsynchronously);
            _deadline = new Deadline(callOptions.Deadline);
            _connectionTimeout = connectionTimeout;
            _method = method;
            _request = request;
        }

        public Task<Metadata> ResponseHeadersAsync => _responseHeadersTcs.Task;

        public async Task<TResponse> GetResponseAsync()
        {
            using var combined = CancellationTokenSource.CreateLinkedTokenSource(_callOptions.CancellationToken, _deadline.Token);
            try
            {
                await _pipeStream.ConnectAsync(_connectionTimeout, combined.Token)
                                 .ConfigureAwait(false);

                _pipeStream.ReadMode = PipeTransmissionMode.Message;

                Message message = MessageBuilder.BuildRequest(_method, _callOptions.Deadline, _callOptions.Headers);

                FrameInfo<TRequest> frameInfo = new(message, _request, _method.RequestMarshaller.ContextualSerializer);

                await _transport.SendFrame(frameInfo, combined.Token)
                                .ConfigureAwait(false);

                _cancelReg = _callOptions.CancellationToken.Register(DisposeCall);
                TResponse ret = await ReadResponsePayload(combined.Token).ConfigureAwait(false);
                return ret;
            }
            catch (Exception ex)
            {
                if (ex is TimeoutException timeoutError)
                    throw new RpcException(new Status(StatusCode.Unavailable, timeoutError.Message));

                if (ex is IOException ioError)
                    throw new RpcException(new Status(StatusCode.Unavailable, $"failed to connect to all addresses:{ioError.Message}"));

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
                _pipeStream.Dispose();
                _transport.Dispose();
            }
        }

        public Metadata GetTrailers() => _responseTrailers ?? throw new InvalidOperationException();

        public Status GetStatus() => _responseTrailers != null ? _status : throw new InvalidOperationException();

        public void DisposeCall()
        {
            try
            {
                //_transport.SendCancelRequest();

                _pipeStream.Dispose();
                _cancelReg.Dispose();
            }
            catch (Exception)
            {
                // Assume the connection is already terminated
            }
        }

        private async Task<TResponse> ReadResponsePayload(CancellationToken token)
        {
            while (_pipeStream.IsConnected && token.IsCancellationRequested == false)
            {
                Frame frame = await _transport.ReadFrame(token).ConfigureAwait(false);
                switch (frame.Message.DataCase)
                {
                    case Message.DataOneofCase.Response:
                        var trailers = MessageBuilder.ToMetadata(frame.Message.Response.Trailers.Metadata);
                        var status = new Status((StatusCode)frame.Message.Response.Trailers.StatusCode, frame.Message.Response.Trailers.StatusDetail);

                        EnsureResponseHeadersSet();
                        _responseTrailers = trailers ?? new Metadata();
                        _status = status;

                        _pipeStream.Close();

                        if (status.StatusCode == StatusCode.OK)
                        {
                            TResponse ret = frame.GetPayload(_method.ResponseMarshaller.ContextualDeserializer);
                            return ret;
                        }
                        throw new RpcException(status);

                    case Message.DataOneofCase.ResponseHeaders:
                        var headerMetadata = MessageBuilder.ToMetadata(frame.Message.ResponseHeaders.Metadata);
                        EnsureResponseHeadersSet(headerMetadata);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            throw new InvalidProgramException();
        }

        private void EnsureResponseHeadersSet(Metadata headers = null)
        {
            _responseHeadersTcs.TrySetResult(headers ?? new Metadata());
        }
    }
}