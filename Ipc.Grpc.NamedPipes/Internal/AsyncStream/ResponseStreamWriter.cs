
using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.TransportProtocol;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class ResponseStreamWriter<TResponse> : IServerStreamWriter<TResponse> where TResponse : class
    {
        private readonly Func<bool> _isCompleted;
        private readonly CancellationToken _cancellationToken;
        private readonly Action<TResponse, SerializationContext> _payloadSerializer;
        private readonly NamedPipeTransportV3 _transport;

        public ResponseStreamWriter(NamedPipeTransportV3 transport, CancellationToken cancellationToken, Action<TResponse, SerializationContext> payloadSerializer, Func<bool> isCompleted)
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

            Message message = new() { RequestControl = Control.StreamMessage }; //TODO: cache this type of message

            FrameInfo<TResponse> frameInfo = new(message, response, _payloadSerializer);
            return _transport.SendFrame(frameInfo, _cancellationToken).AsTask();
        }
    }
}