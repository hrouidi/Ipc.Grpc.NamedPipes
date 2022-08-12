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
            _listener = ServerListenerFactory.Create(pipeName, options, _binder.MethodHandlers);
        }

        public NamedPipeServer AddInterceptor(Interceptor interceptor)
        {
            _binder.Interceptors.Add(interceptor);
            return this;
        }

        /// <summary>
        /// Start server listening and yield control to the caller
        /// </summary>
        public Task StartAsync() => _listener.StartAsync();

        /// <summary>
        /// Start server listening loop , will yield when gracefully shutdown or dispose
        /// </summary>
        /// <returns></returns>
        public Task RunAsync() => _listener.RunAsync();

        /// <summary>
        /// Gracefully shutdown the server, will wait for all pending connections to complete
        /// </summary>
        public Task ShutdownAsync() => _listener.ShutdownAsync();

        /// <summary>
        /// Stop the server and cancel all pending connections handlers
        /// </summary>
        public void Dispose() => _listener.Dispose();

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