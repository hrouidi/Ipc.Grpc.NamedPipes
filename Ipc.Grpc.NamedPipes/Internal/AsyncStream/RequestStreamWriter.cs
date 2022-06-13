using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.TransportProtocol;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class RequestStreamWriter<TRequest> : IClientStreamWriter<TRequest> where TRequest : class
    {
        private readonly CancellationToken _connectionCancellationToken;
        private readonly Action<TRequest, SerializationContext> _payloadSerializer;
        private readonly Transport _transport;

        private readonly Task<Exception> _sendTask;

        private bool _isCompleted;

        public RequestStreamWriter(Transport transport, Action<TRequest, SerializationContext> payloadSerializer, CancellationToken connectionCancellationToken, Task<Exception> sendTask)
        {
            _transport = transport;
            _payloadSerializer = payloadSerializer;
            _connectionCancellationToken = connectionCancellationToken;
            _sendTask = sendTask;
        }

        public WriteOptions WriteOptions { get; set; }


        public Task CompleteAsync()
        {
            _isCompleted = true;
            Message message = new() { RequestControl = Control.StreamMessageEnd }; //TODO: cache this type of message
            return _transport.SendFrame(message, _connectionCancellationToken).AsTask();
        }

        public async Task WriteAsync(TRequest payload)
        {
            if (_isCompleted)
                throw new InvalidOperationException("Request stream has already been completed.");

            // Case when request send fails
            Exception exception = await _sendTask.ConfigureAwait(false);
            if (exception != null)
                throw exception;

            Message message = new() { RequestControl = Control.StreamMessage }; //TODO: cache this type of message
            FrameInfo<TRequest> frameInfo = new(message, payload, _payloadSerializer);
            await _transport.SendFrame(frameInfo, _connectionCancellationToken);
        }
    }
}