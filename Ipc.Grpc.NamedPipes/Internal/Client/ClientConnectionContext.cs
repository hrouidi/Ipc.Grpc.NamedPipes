using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Protocol;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class ClientConnectionContext<TRequest, TResponse> : IServerMessageHandler, IDisposable
    {
        private readonly NamedPipeClientStream _pipeStream;
        private readonly CallOptions _callOptions;
        private readonly PayloadChannel<byte[]> _payloadChannel;
        private readonly Deadline _deadline;
        private readonly int _connectionTimeout;
        private readonly Method<TRequest, TResponse> _method;
        private readonly TRequest _request;

        private readonly TaskCompletionSource<Metadata> _responseHeadersTcs;

        private CancellationTokenRegistration _cancelReg;
        private Metadata _responseTrailers;
        private Status _status;

        public ClientConnectionContext(NamedPipeClientStream pipeStream, CallOptions callOptions, int connectionTimeout, Method<TRequest, TResponse> method, TRequest request)
        {
            _pipeStream = pipeStream;
            _callOptions = callOptions;
            Transport = new NamedPipeTransport(_pipeStream);
            _responseHeadersTcs = new TaskCompletionSource<Metadata>(TaskCreationOptions.RunContinuationsAsynchronously);
            _payloadChannel = new PayloadChannel<byte[]>();
            _deadline = new Deadline(callOptions.Deadline);
            _connectionTimeout = connectionTimeout;
            _method = method;
            _request = request;
        }

        public void Init()
        {
            try
            {
                if (!_callOptions.CancellationToken.IsCancellationRequested && !_deadline.IsExpired)
                {
                    _pipeStream.Connect(_connectionTimeout);
                    _pipeStream.ReadMode = PipeTransmissionMode.Message;

                    Transport.SendRequest(_method, _request, _callOptions.Deadline, _callOptions.Headers);
                    _cancelReg = _callOptions.CancellationToken.Register(DisposeCall);
                }
                Task.Run(ReadLoop);
            }
            catch (Exception ex)
            {
                _pipeStream.Dispose();

                if (ex is TimeoutException or IOException)
                {
                    throw new RpcException(new Status(StatusCode.Unavailable, "failed to connect to all addresses"));
                }

                throw;
            }
        }

        public NamedPipeTransport Transport { get; }

        public Task<Metadata> ResponseHeadersAsync => _responseHeadersTcs.Task;

        public void HandleResponseHeaders(Headers headers)
        {
            var headerMetadata = OldMessageBuilder.ToMetadata(headers.Metadata);
            EnsureResponseHeadersSet(headerMetadata);
        }

        public async ValueTask HandleResponseAsync(Response response, byte[] payload)
        {
            var trailers = OldMessageBuilder.ToMetadata(response.Trailers.Metadata);
            var status = new Status((StatusCode)response.Trailers.StatusCode, response.Trailers.StatusDetail);

            EnsureResponseHeadersSet();
            _responseTrailers = trailers ?? new Metadata();
            _status = status;

            _pipeStream.Close();

            if (status.StatusCode == StatusCode.OK)
            {
                if (payload != null)
                    await _payloadChannel.Append(payload);
                else //: response streaming
                    await _payloadChannel.SetCompleted();
            }
            else
            {
                await _payloadChannel.SetError(new RpcException(status));
            }
        }

        public async Task<TResponse> ReadUnaryResponseAsync(Marshaller<TResponse> responseMarshaller)
        {
            try
            {
                using var combined = CancellationTokenSource.CreateLinkedTokenSource(_callOptions.CancellationToken, _deadline.Token);
                byte[] ret = await _payloadChannel.ReadAsync(combined.Token).ConfigureAwait(false);
                return SerializationHelpers.Deserialize(responseMarshaller, ret);
            }
            catch (OperationCanceledException)
            {
                if (_deadline.IsExpired)
                    throw new RpcException(new Status(StatusCode.DeadlineExceeded, ""));
                throw new RpcException(Status.DefaultCancelled);
            }
        }

        
        public ValueTask HandleResponseStreamPayload(byte[] payload)
        {
            EnsureResponseHeadersSet();
            return _payloadChannel.Append(payload);
        }

        private void EnsureResponseHeadersSet(Metadata headers = null)
        {
            _responseHeadersTcs.TrySetResult(headers ?? new Metadata());
        }

        public Metadata GetTrailers() => _responseTrailers ?? throw new InvalidOperationException();

        public Status GetStatus() => _responseTrailers != null ? _status : throw new InvalidOperationException();


        public MessageStreamReader<TResponse> GetResponseStreamReader(Marshaller<TResponse> responseMarshaller)
        {
            return new MessageStreamReader<TResponse>(_payloadChannel, responseMarshaller, _callOptions.CancellationToken, _deadline);
        }

        public IClientStreamWriter<TRequest> GetRequestStreamWriter(Marshaller<TRequest> requestMarshaller)
        {
            return new RequestStreamWriter<TRequest>(Transport, _callOptions.CancellationToken, requestMarshaller);
        }

        public void DisposeCall()
        {
            try
            {
                Transport.SendCancelRequest();
            }
            catch (Exception)
            {
                // Assume the connection is already terminated
            }
        }

        public void Dispose()
        {
            _pipeStream.Dispose();
            _cancelReg.Dispose();
        }

        #region Houssam

        public async Task ReadLoop()
        {
            try
            {
                while (_pipeStream.IsConnected)
                {
                    await Transport.ReadServerMessages(this).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // TODO: Log if unexpected
            }
            finally
            {
                Dispose();
            }
        }

        #endregion
    }
}