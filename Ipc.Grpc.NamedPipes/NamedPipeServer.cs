using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Ipc.Grpc.NamedPipes.Internal;

namespace Ipc.Grpc.NamedPipes
{
    public class NamedPipeServer : IDisposable
    {
        private readonly NamedPipesServiceBinder _binder;
        private readonly ServerListener _listener;

        public ServiceBinderBase ServiceBinder => _binder;

        public NamedPipeServer(string pipeName) : this(pipeName, NamedPipeServerOptions.Default) { }
        public NamedPipeServer(string pipeName, NamedPipeServerOptions options)
        {
            _binder = new NamedPipesServiceBinder();
            _listener = new ServerListener(pipeName, options, _binder.MethodHandlers);
        }

        public NamedPipeServer AddInterceptor(Interceptor interceptor)
        {
            _binder.Interceptors.Add(interceptor);
            return this;
        }

        public void Run()
        {
            _listener.Start();
            _listener.ListeningTask
                     .GetAwaiter()
                     .GetResult();
        }

        public Task RunAsync()
        {
            _listener.Start();
            return _listener.ListeningTask;
        }

        public void Start()
        {
            _listener.Start();
        }

        public ValueTask StartAsync()
        {
            return _listener.StartAsync();
        }

        public void Shutdown()
        {
            _listener.Stop();
        }

        public ValueTask ShutdownAsync()
        {
            return _listener.StopAsync();
        }

        public void Kill()
        {
            _listener.Dispose();
        }

        public void Dispose()
        {
            _listener.Dispose();
        }

        private class NamedPipesServiceBinder : ServiceBinderBase
        {
            internal readonly Dictionary<string, Func<ServerConnection, ValueTask>> MethodHandlers;
            internal readonly List<Interceptor> Interceptors;

            public NamedPipesServiceBinder()
            {
                MethodHandlers = new Dictionary<string, Func<ServerConnection, ValueTask>>();
                Interceptors = new List<Interceptor>();
            }

            public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler)
                where TRequest : class
                where TResponse : class
            {
                async ValueTask Handle(ServerConnection connection)
                {
                    try
                    {
                        TRequest request = connection.GetUnaryRequest(method.RequestMarshaller);

                        UnaryServerMethod<TRequest, TResponse> pipeline = Interceptors.Aggregate(handler, (current, interceptor) => (req, ctx) => interceptor.UnaryServerHandler(req, ctx, current));
                        TResponse response = await pipeline(request, connection.CallContext).ConfigureAwait(false);

                        await connection.Success(method.ResponseMarshaller, response)
                                        .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await connection.Error(ex)
                                        .ConfigureAwait(false);
                    }
                }

                MethodHandlers.Add(method.FullName, Handle);
            }

            public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ClientStreamingServerMethod<TRequest, TResponse> handler)
                where TRequest : class
                where TResponse : class
            {
                async ValueTask Handle(ServerConnection connection)
                {
                    try
                    {
                        IAsyncStreamReader<TRequest> requestStreamReader = connection.GetRequestStreamReader(method.RequestMarshaller);

                        ClientStreamingServerMethod<TRequest, TResponse> pipeline = Interceptors.Aggregate(handler, (current, interceptor) => (req, ctx) => interceptor.ClientStreamingServerHandler(req, ctx, current));
                        TResponse response = await pipeline(requestStreamReader, connection.CallContext).ConfigureAwait(false);

                        await connection.Success(method.ResponseMarshaller, response).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await connection.Error(ex).ConfigureAwait(false);
                    }
                }

                MethodHandlers.Add(method.FullName, Handle);
            }

            public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ServerStreamingServerMethod<TRequest, TResponse> handler)
                where TRequest : class
                where TResponse : class
            {
                async ValueTask Handle(ServerConnection connection)
                {
                    try
                    {
                        TRequest request = connection.GetUnaryRequest(method.RequestMarshaller);

                        IServerStreamWriter<TResponse> responseStreamReader = connection.GetResponseStreamWriter(method.ResponseMarshaller);

                        ServerStreamingServerMethod<TRequest, TResponse> pipeline = Interceptors.Aggregate(handler, (current, interceptor) => (req, rep, ctx) => interceptor.ServerStreamingServerHandler(req, rep, ctx, current));
                        await pipeline(request, responseStreamReader, connection.CallContext).ConfigureAwait(false);

                        await connection.Success<TResponse>()
                                        .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await connection.Error(ex)
                                        .ConfigureAwait(false);
                    }
                }

                MethodHandlers.Add(method.FullName, Handle);
            }

            public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, DuplexStreamingServerMethod<TRequest, TResponse> handler)
                where TRequest : class
                where TResponse : class
            {
                async ValueTask Handle(ServerConnection connection)
                {
                    try
                    {
                        var requestStreamReader = connection.GetRequestStreamReader(method.RequestMarshaller);
                        var responseStreamReader = connection.GetResponseStreamWriter(method.ResponseMarshaller);

                        var pipeline = Interceptors.Aggregate(handler, (current, interceptor) => (req, rep, ctx) => interceptor.DuplexStreamingServerHandler(req, rep, ctx, current));
                        await pipeline(requestStreamReader, responseStreamReader, connection.CallContext).ConfigureAwait(false);

                        await connection.Success<TResponse>()
                                        .ConfigureAwait(false); ;
                    }
                    catch (Exception ex)
                    {
                        await connection.Error(ex)
                                        .ConfigureAwait(false);
                    }
                }

                MethodHandlers.Add(method.FullName, Handle);
            }
        }
    }
}