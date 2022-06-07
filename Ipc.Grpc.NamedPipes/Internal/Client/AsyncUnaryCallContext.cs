using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Protocol;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class AsyncUnaryCallContext<TRequest, TResponse>
    {
        private readonly NamedPipeClientStream _pipeStream;
        private readonly CallOptions _callOptions;
        private readonly Deadline _deadline;
        private readonly int _connectionTimeout;
        private readonly Method<TRequest, TResponse> _method;
        private readonly TRequest _request;

        private readonly TaskCompletionSource<Metadata> _responseHeadersTcs;
        private readonly NamedPipeTransport _transport;

        private CancellationTokenRegistration _cancelReg;
        private Metadata _responseTrailers;
        private Status _status;

        public AsyncUnaryCallContext(NamedPipeClientStream pipeStream, CallOptions callOptions, int connectionTimeout, Method<TRequest, TResponse> method, TRequest request)
        {
            _pipeStream = pipeStream;
            _callOptions = callOptions;
            _transport = new NamedPipeTransport(_pipeStream);
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
                //_pipeStream.ConnectAsync(_connectionTimeout);
                _pipeStream.ReadMode = PipeTransmissionMode.Message;
                await _transport.SendUnaryRequest(_method, _request, _callOptions.Deadline, _callOptions.Headers, combined.Token)
                                .ConfigureAwait(false);
                _cancelReg = _callOptions.CancellationToken.Register(DisposeCall);
                using MemoryStream payloadStream = await ReadResponsePayload(combined.Token)
                                                        .ConfigureAwait(false);
                return SerializationHelpers.Deserialize(_method.ResponseMarshaller, payloadStream);
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
            }
        }

        public Metadata GetTrailers() => _responseTrailers ?? throw new InvalidOperationException();

        public Status GetStatus() => _responseTrailers != null ? _status : throw new InvalidOperationException();

        public void DisposeCall()
        {
            try
            {
                _transport.SendCancelRequest();

                _pipeStream.Dispose();
                _cancelReg.Dispose();
            }
            catch (Exception)
            {
                // Assume the connection is already terminated
            }
        }

        private async Task<MemoryStream> ReadResponsePayload(CancellationToken token)
        {
            while (_pipeStream.IsConnected && token.IsCancellationRequested == false)
            {
                ServerResponse rep = await _transport.ReadServerMessagesAsync(token)
                                                     .ConfigureAwait(false);
                switch (rep.Type)
                {
                    case ServerMessage.DataOneofCase.Response:
                        var trailers = TransportMessageBuilder.ToMetadata(rep.Response.Trailers.Metadata);
                        var status = new Status((StatusCode)rep.Response.Trailers.StatusCode, rep.Response.Trailers.StatusDetail);

                        EnsureResponseHeadersSet();
                        _responseTrailers = trailers ?? new Metadata();
                        _status = status;

                        _pipeStream.Close();

                        if (status.StatusCode == StatusCode.OK)
                            return rep.Payload;

                        throw new RpcException(status);
                    case ServerMessage.DataOneofCase.ResponseHeaders:
                        var headerMetadata = TransportMessageBuilder.ToMetadata(rep.Headers.Metadata);
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