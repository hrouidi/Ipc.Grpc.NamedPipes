using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.TransportProtocol;
using Request = Ipc.Grpc.NamedPipes.Protocol.Request;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal class ServerConnectionContext : IClientMessageHandler, IDisposable
    {
        private readonly IReadOnlyDictionary<string, Func<ServerConnectionContext, ValueTask>> _methodHandlers;
        private readonly PayloadChannel<byte[]> _payloadChannel;

        public ServerConnectionContext(NamedPipeServerStream pipeStream, IReadOnlyDictionary<string, Func<ServerConnectionContext, ValueTask>> methodHandlers)
        {
            CallContext = new NamedPipeCallContext(this);
            PipeStream = pipeStream;
            Transport = new NamedPipeTransport(pipeStream);
            _methodHandlers = methodHandlers;
            _payloadChannel = new PayloadChannel<byte[]>();
            CancellationTokenSource = new CancellationTokenSource();
        }

        public void Dispose()
        {
            try
            {
                PipeStream.Disconnect();
                PipeStream.Dispose();
                Transport.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error : {ex.Message}");
            }
        }

        public NamedPipeServerStream PipeStream { get; }

        public NamedPipeTransport Transport { get; }

        public CancellationTokenSource CancellationTokenSource { get; }

        public Deadline Deadline { get; private set; }

        public Metadata RequestHeaders { get; private set; }

        public ServerCallContext CallContext { get; }

        public bool IsCompleted { get; private set; }

        public IAsyncStreamReader<TRequest> GetRequestStreamReader<TRequest>(Marshaller<TRequest> requestMarshaller)
        {
            return new MessageStreamReader<TRequest>(_payloadChannel, requestMarshaller, CancellationToken.None, Deadline);
        }

        public IServerStreamWriter<TResponse> GetResponseStreamWriter<TResponse>(Marshaller<TResponse> responseMarshaller)
        {
            return new ResponseStreamWriter<TResponse>(Transport, CancellationToken.None, responseMarshaller, () => IsCompleted);
        }

        public void Error(Exception ex)
        {
            IsCompleted = true;
            (StatusCode status, string detail) = GetStatus();
            Transport.SendResponse(null, CallContext.ResponseTrailers, status, detail);

            (StatusCode status, string detail) GetStatus()
            {
                if (Deadline is { IsExpired: true })
                    return (StatusCode.DeadlineExceeded, "");

                if (CancellationTokenSource.IsCancellationRequested)
                    return (StatusCode.Cancelled, "");

                if (ex is RpcException rpcException)
                    return (rpcException.StatusCode, rpcException.Status.Detail);

                return (StatusCode.Unknown, "Exception was thrown by handler.");
            }
        }

        public void Success(byte[] responsePayload = null)
        {
            IsCompleted = true;
            (StatusCode status, string detail) = CallContext.Status.StatusCode switch
            {
                StatusCode.OK => (StatusCode.OK, ""),
                _ => (CallContext.Status.StatusCode, CallContext.Status.Detail)
            };
            Transport.SendResponse(responsePayload, CallContext.ResponseTrailers, status, detail);
        }

        public async ValueTask UnarySuccess<T>(Marshaller<T> marshaller, T response)
        {
            IsCompleted = true;
            (StatusCode status, string detail) = CallContext.Status.StatusCode switch
            {
                StatusCode.OK => (StatusCode.OK, ""),
                _ => (CallContext.Status.StatusCode, CallContext.Status.Detail)
            };
            await Transport.SendUnaryResponse(marshaller, response, CallContext.ResponseTrailers, status, detail, CallContext.CancellationToken)
                           .ConfigureAwait(false);
            // Stop listening
            _continueListening = false;
        }

        private bool _continueListening = true;
        public async ValueTask UnaryError(Exception ex)
        {
            IsCompleted = true;
            (StatusCode status, string detail) = GetStatus();
            await Transport.SendUnaryResponse(CallContext.ResponseTrailers, status, detail, CallContext.CancellationToken)
                           .ConfigureAwait(false);
            // Stop listening
            _continueListening = false;
            (StatusCode status, string detail) GetStatus()
            {
                if (Deadline is { IsExpired: true })
                    return (StatusCode.DeadlineExceeded, "");

                if (CancellationTokenSource.IsCancellationRequested)
                    return (StatusCode.Cancelled, "");

                if (ex is RpcException rpcException)
                    return (rpcException.StatusCode, rpcException.Status.Detail);

                return (StatusCode.Unknown, "Exception was thrown by handler.");
            }
        }

       

        #region Houssam

        public async Task ListenMessagesAsync(CancellationToken shutdownToken)
        {
            while (_continueListening && PipeStream.IsConnected && shutdownToken.IsCancellationRequested == false)
            {
                await Transport.ReadClientMessages(this).ConfigureAwait(false);
            }
        }

        private Request _request;
        private byte[] _payload;

        public void HandleRequest(Request message, byte[] payload)
        {
            _request = message;
            _payload = payload;
            Deadline = new Deadline(message.Deadline?.ToDateTime());
            RequestHeaders = TransportMessageBuilder.ToMetadata(message.Headers.Metadata);
            Task.Run(async () => await _methodHandlers[message.MethodFullName](this).ConfigureAwait(false));
        }

        private MemoryStream _payloadStream;

        private Memory<byte> _payloadStream2;
        private IMemoryOwner<byte> _owner;

        public async ValueTask HandleUnaryRequest(Request message, MemoryStream payload)
        {
            _request = message;
            _payloadStream = payload;

            Deadline = new Deadline(message.Deadline?.ToDateTime());
            RequestHeaders = TransportMessageBuilder.ToMetadata(message.Headers.Metadata);
            //Task.Run(async () => await _methodHandlers[message.MethodFullName](this).ConfigureAwait(false));
            await _methodHandlers[message.MethodFullName](this).ConfigureAwait(false);
        }

        public async ValueTask HandleUnaryRequest2(TransportProtocol.Request message, Memory<byte> payload, IMemoryOwner<byte> owner)
        {
            //_request = message;
            _payloadStream2 = payload;
            _owner = owner;
            Deadline = new Deadline(message.Deadline?.ToDateTime());
            //RequestHeaders = TransportMessageBuilder.ToMetadata(message.Headers.Metadata);
            //Task.Run(async () => await _methodHandlers[message.MethodFullName](this).ConfigureAwait(false));
            Func<ServerConnectionContext, ValueTask> executor = _methodHandlers[message.MethodFullName];
            await executor(this).ConfigureAwait(false);
        }

        public ValueTask HandleRequestStreamPayload(byte[] payload) => _payloadChannel.Append(payload);

        public ValueTask HandleRequestStreamEnd()
        {
            return _payloadChannel.SetCompleted();
        }

        public void HandleCancel()
        {
            CancellationTokenSource.Cancel();
        }

        public TRequest GetRequest<TRequest>(Marshaller<TRequest> requestMarshaller)
        {
            return SerializationHelpers.Deserialize(requestMarshaller, _payload);
        }

        public TRequest GetUnaryRequest<TRequest>(Marshaller<TRequest> requestMarshaller)
        {
            TRequest ret = SerializationHelpers.Deserialize(requestMarshaller, _payloadStream);
            _payloadStream.Dispose();
            return ret;
        }

        public TRequest GetUnaryRequest2<TRequest>(Marshaller<TRequest> requestMarshaller)
        {
            var deserializationContext = new MemoryDeserializationContext(_payloadStream2);
            TRequest ret = requestMarshaller.ContextualDeserializer(deserializationContext);
            return ret;
        }

        #endregion
    }
}