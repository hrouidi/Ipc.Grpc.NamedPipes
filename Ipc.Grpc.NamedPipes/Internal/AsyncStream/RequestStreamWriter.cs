using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class RequestStreamWriter<TRequest> : IClientStreamWriter<TRequest>
    {
        private readonly NamedPipeTransport _stream;
        private bool _isCompleted;
        private readonly CancellationToken _cancellationToken;
        private readonly Marshaller<TRequest> _marshaller;

        public RequestStreamWriter(NamedPipeTransport stream, CancellationToken cancellationToken, Marshaller<TRequest> marshaller)
        {
            _stream = stream;
            _cancellationToken = cancellationToken;
            _marshaller = marshaller;
        }

        public WriteOptions WriteOptions { get; set; }


        public Task CompleteAsync()
        {
            _stream.SendRequestPayloadStreamEnd();
            _isCompleted = true;
            return Task.CompletedTask;
        }

        public virtual Task WriteAsync(TRequest message)
        {
            if (_isCompleted)
                throw new InvalidOperationException($"Request stream has already been completed.");

            if (_cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(_cancellationToken);

            _stream.SendStreamRequestPayload(_marshaller, message);
            return Task.CompletedTask;
        }
    }
}