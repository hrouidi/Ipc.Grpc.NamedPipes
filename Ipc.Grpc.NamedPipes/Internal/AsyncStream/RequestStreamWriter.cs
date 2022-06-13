using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal.Transport;
using Message = Ipc.Grpc.NamedPipes.Internal.Transport.Message;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class RequestStreamWriter<TRequest> : IClientStreamWriter<TRequest> where TRequest : class
    {
        private readonly CancellationToken _connectionCancellationToken;
        private readonly Action<TRequest, SerializationContext> _payloadSerializer;
        private readonly NamedPipeTransport _transport;

        private readonly Task<Exception> _sendTask;

        private bool _isCompleted;

        public RequestStreamWriter(NamedPipeTransport transport, Action<TRequest, SerializationContext> payloadSerializer, CancellationToken connectionCancellationToken, Task<Exception> sendTask)
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
            Message message = MessageBuilder.StreamEnd; //TODO: cache this type of message
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

            FrameInfo <TRequest> frameInfo = new(MessageBuilder.Streaming, payload, _payloadSerializer);
            await _transport.SendFrame(frameInfo, _connectionCancellationToken);
        }
    }
}