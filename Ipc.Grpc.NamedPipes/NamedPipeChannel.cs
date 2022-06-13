using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
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
            var connection = new ClientConnection<TRequest, TResponse>(stream, callOptions, _options.ConnectionTimeout, method, request);
            return connection.GetResponseAsync()
                             .GetAwaiter()
                             .GetResult();
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host,
            CallOptions callOptions, TRequest request) where TRequest : class where TResponse : class
        {
            NamedPipeClientStream stream = CreatePipeStream();
            var connection = new ClientConnection<TRequest, TResponse>(stream, callOptions, _options.ConnectionTimeout, method, request);

            return new AsyncUnaryCall<TResponse>(
                connection.GetResponseAsync(),
                ResponseHeadersAsync<TRequest, TResponse>,
                GetStatus<TRequest, TResponse>,
                GetTrailers<TRequest, TResponse>,
                Dispose<TRequest, TResponse>,
                connection);
        }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions callOptions,
            TRequest request) where TRequest : class where TResponse : class
        {
            NamedPipeClientStream stream = CreatePipeStream();
            var connection = new ClientConnection<TRequest, TResponse>(stream, callOptions, _options.ConnectionTimeout, method, request);

            return new AsyncServerStreamingCall<TResponse>(
                connection.GetResponseStreamReader(),
                ResponseHeadersAsync<TRequest, TResponse>,
                GetStatus<TRequest, TResponse>,
                GetTrailers<TRequest, TResponse>,
                Dispose<TRequest, TResponse>,
                connection);
        }

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions callOptions) where TRequest : class where TResponse : class
        {
            NamedPipeClientStream stream = CreatePipeStream();
            var connection = new ClientConnection<TRequest, TResponse>(stream, callOptions, _options.ConnectionTimeout, method, null);

            return new AsyncClientStreamingCall<TRequest, TResponse>(
                connection.GetRequestStreamWriter(),
                connection.GetResponseAsync(),
                ResponseHeadersAsync<TRequest, TResponse>,
                GetStatus<TRequest, TResponse>,
                GetTrailers<TRequest, TResponse>,
                Dispose<TRequest, TResponse>,
                connection);
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions callOptions) where TRequest : class where TResponse : class
        {
            NamedPipeClientStream stream = CreatePipeStream();
            var connection = new ClientConnection<TRequest, TResponse>(stream, callOptions, _options.ConnectionTimeout, method, null);

            return new AsyncDuplexStreamingCall<TRequest, TResponse>(
                connection.GetRequestStreamWriter(),
                connection.GetResponseStreamReader(),
                ResponseHeadersAsync<TRequest, TResponse>,
                GetStatus<TRequest, TResponse>,
                GetTrailers<TRequest, TResponse>,
                Dispose<TRequest, TResponse>,
                connection);
        }

        private static Task<Metadata> ResponseHeadersAsync<TRequest, TResponse>(object x) where TRequest : class where TResponse : class
            => ((ClientConnection<TRequest, TResponse>)x).ResponseHeadersAsync;

        private static Status GetStatus<TRequest, TResponse>(object x) where TRequest : class where TResponse : class
            => ((ClientConnection<TRequest, TResponse>)x).GetStatus();

        private static Metadata GetTrailers<TRequest, TResponse>(object x) where TRequest : class where TResponse : class
            => ((ClientConnection<TRequest, TResponse>)x).GetTrailers();

        private static void Dispose<TRequest, TResponse>(object x) where TRequest : class where TResponse :
            class => ((ClientConnection<TRequest, TResponse>)x).DisposeCall();
    }
}