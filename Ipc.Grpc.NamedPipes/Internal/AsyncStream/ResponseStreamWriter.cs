
using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class ResponseStreamWriter<TResponse> :  IServerStreamWriter<TResponse>
    {
        private readonly Func<bool> _isCompleted;
        private readonly CancellationToken _cancellationToken;
        private readonly Marshaller<TResponse> _marshaller;
        private readonly NamedPipeTransport _stream;

        public ResponseStreamWriter(NamedPipeTransport stream, CancellationToken cancellationToken, Marshaller<TResponse> marshaller, Func<bool> isCompleted)
        {
            _stream = stream;
            _cancellationToken = cancellationToken;
            _marshaller = marshaller;
            _isCompleted = isCompleted;
        }

        public WriteOptions WriteOptions { get; set; }

        public  Task WriteAsync(TResponse response)
        {
            if (_isCompleted())
                throw new InvalidOperationException("Response stream has already been completed.");

            if (_cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(_cancellationToken);

            _stream.SendStreamResponsePayload(_marshaller, response);
            return Task.CompletedTask;
        }

    }
}