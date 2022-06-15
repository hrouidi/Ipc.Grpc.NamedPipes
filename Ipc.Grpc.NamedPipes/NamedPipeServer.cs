using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal;

namespace Ipc.Grpc.NamedPipes
{
    public class NamedPipeServer : IDisposable
    {
        private readonly ServerListener _listener;
        private readonly Dictionary<string, Func<ServerConnection, ValueTask>> _methodHandlers = new();

        public NamedPipeServer(string pipeName) : this(pipeName, NamedPipeServerOptions.Default) { }

        public NamedPipeServer(string pipeName, NamedPipeServerOptions options)
        {
            _listener = new ServerListener(pipeName, options, _methodHandlers);
            ServiceBinder = new ServiceBinderImpl(this);
        }

        public ServiceBinderBase ServiceBinder { get; }

        public void Start()
        {
            _listener.Start();
        }

        public void Stop()
        {
            _listener.Stop();
        }

        public void Kill()
        {
            _listener.Dispose();
        }

        public void Dispose()
        {
            _listener.Dispose();
        }

        private class ServiceBinderImpl : ServiceBinderBase
        {
            private readonly NamedPipeServer _server;

            public ServiceBinderImpl(NamedPipeServer server)
            {
                _server = server;
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
                        
                        TResponse response = await handler(request, connection.CallContext).ConfigureAwait(false);

                        await connection.Success(method.ResponseMarshaller, response)
                                        .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await connection.Error(ex)
                                        .ConfigureAwait(false);
                    }
                }

                _server._methodHandlers.Add(method.FullName, Handle);
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
                        
                        TResponse response = await handler(requestStreamReader, connection.CallContext).ConfigureAwait(false);

                        await connection.Success(method.ResponseMarshaller, response).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await connection.Error(ex).ConfigureAwait(false);
                    }
                }

                _server._methodHandlers.Add(method.FullName, Handle);
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

                        await handler(request, responseStreamReader, connection.CallContext).ConfigureAwait(false);

                        await connection.Success<TResponse>()
                                        .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await connection.Error(ex)
                                        .ConfigureAwait(false);
                    }
                }

                _server._methodHandlers.Add(method.FullName, Handle);
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

                        await handler(requestStreamReader, responseStreamReader, connection.CallContext).ConfigureAwait(false);

                        await connection.Success<TResponse>()
                                        .ConfigureAwait(false); ;
                    }
                    catch (Exception ex)
                    {
                        await connection.Error(ex)
                                        .ConfigureAwait(false);
                    }
                }

                _server._methodHandlers.Add(method.FullName, Handle);
            }
        }
    }
}