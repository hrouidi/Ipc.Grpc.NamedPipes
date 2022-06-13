
using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal.Transport;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class ResponseStreamWriter<TResponse> : IServerStreamWriter<TResponse> where TResponse : class
    {
        private readonly Func<bool> _isCompleted;
        private readonly CancellationToken _cancellationToken;
        private readonly Action<TResponse, SerializationContext> _payloadSerializer;
        private readonly NamedPipeTransport _transport;

        public ResponseStreamWriter(NamedPipeTransport transport, CancellationToken cancellationToken, Action<TResponse, SerializationContext> payloadSerializer, Func<bool> isCompleted)
        {
            _transport = transport;
            _cancellationToken = cancellationToken;
            _payloadSerializer = payloadSerializer;
            _isCompleted = isCompleted;
        }

        public WriteOptions WriteOptions { get; set; }

        public Task WriteAsync(TResponse response)
        {
            if (_isCompleted())
                throw new InvalidOperationException("Response stream has already been completed.");

            if (_cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(_cancellationToken);

            FrameInfo<TResponse> frameInfo = new(MessageBuilder.Streaming, response, _payloadSerializer);
            return _transport.SendFrame(frameInfo, _cancellationToken).AsTask();
        }
    }
}