using System.IO;
using System.IO.Pipes;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal;

namespace Ipc.Grpc.NamedPipes
{
    public class NamedPipeChannel : CallInvoker
    {
        private readonly string _serverName;
        private readonly string _pipeName;
        private readonly NamedPipeChannelOptions _options;

        public NamedPipeChannel(string pipeName, NamedPipeChannelOptions options) 
        {
            _serverName = ".";
            _pipeName = pipeName;
            _options = options;
        }

        private NamedPipeClientStream CreatePipeStream()
        {
            var pipeOptions = PipeOptions.Asynchronous;
#if NETCOREAPP || NETSTANDARD2_1
            if (_options.CurrentUserOnly)
            {
                pipeOptions |= PipeOptions.CurrentUserOnly;
            }
#endif
            var stream = new NamedPipeClientStream(_serverName,
                _pipeName,
                PipeDirection.InOut,
                pipeOptions,
                _options.ImpersonationLevel,
                HandleInheritability.None);
            return stream;
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
            string host, CallOptions callOptions, TRequest request) where TRequest : class where TResponse : class
        {
            NamedPipeClientStream stream = CreatePipeStream();
            var ctx = new ClientConnection<TRequest, TResponse>(stream, callOptions, _options.ConnectionTimeout, method, request);
            return ctx.GetResponseAsync().Result;
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, 
            CallOptions callOptions, TRequest request) where TRequest : class where TResponse : class
        {
            NamedPipeClientStream stream = CreatePipeStream();
            var ctx = new ClientConnection<TRequest, TResponse>(stream, callOptions, _options.ConnectionTimeout, method, request);
            return new AsyncUnaryCall<TResponse>(
                ctx.GetResponseAsync(),
                ctx.ResponseHeadersAsync,
                ctx.GetStatus,
                ctx.GetTrailers,
                ctx.DisposeCall);
        }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions callOptions,
            TRequest request) where TRequest : class where TResponse : class
        {
            NamedPipeClientStream stream = CreatePipeStream();
            var ctx = new ClientConnection<TRequest, TResponse>(stream, callOptions, _options.ConnectionTimeout, method, request);
            return new AsyncServerStreamingCall<TResponse>(
                ctx.GetResponseStreamReader(),
                ctx.ResponseHeadersAsync,
                ctx.GetStatus,
                ctx.GetTrailers,
                ctx.DisposeCall);
        }

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions callOptions) where TRequest : class where TResponse : class
        {
            NamedPipeClientStream stream = CreatePipeStream();
            var ctx = new ClientConnection<TRequest, TResponse>(stream, callOptions, _options.ConnectionTimeout, method, null);
            return new AsyncClientStreamingCall<TRequest, TResponse>(
                ctx.GetRequestStreamWriter(),
                ctx.GetResponseAsync(),
                ctx.ResponseHeadersAsync,
                ctx.GetStatus,
                ctx.GetTrailers,
                ctx.DisposeCall);
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions callOptions) where TRequest : class where TResponse : class
        {
            NamedPipeClientStream stream = CreatePipeStream();
            var ctx = new ClientConnection<TRequest, TResponse>(stream, callOptions, _options.ConnectionTimeout, method, null);
            return new AsyncDuplexStreamingCall<TRequest, TResponse>(
                ctx.GetRequestStreamWriter2(),
                ctx.GetResponseStreamReader2(),
                ctx.ResponseHeadersAsync,
                ctx.GetStatus,
                ctx.GetTrailers,
                ctx.DisposeCall);
        }
    }
}