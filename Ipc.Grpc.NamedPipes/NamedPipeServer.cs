using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Internal;

namespace Ipc.Grpc.NamedPipes
{
    public class NamedPipeServer : IDisposable
    {
        private readonly ServerStreamPool _pool;
        private readonly Dictionary<string, Func<ServerConnectionContext, ValueTask>> _methodHandlers = new();

        public NamedPipeServer(string pipeName) : this(pipeName, NamedPipeServerOptions.Default) { }

        public NamedPipeServer(string pipeName, NamedPipeServerOptions options)
        {
            _pool = new ServerStreamPool(pipeName, options, _methodHandlers);
            ServiceBinder = new ServiceBinderImpl(this);
        }

        public ServiceBinderBase ServiceBinder { get; }

        public void Start()
        {
            _pool.Start();
        }

        public void Stop()
        {
            _pool.Stop();
        }

        public void Kill()
        {
            _pool.Dispose();
        }

        public void Dispose()
        {
            _pool.Dispose();
        }

        private class ServiceBinderImpl : ServiceBinderBase
        {
            private readonly NamedPipeServer _server;

            public ServiceBinderImpl(NamedPipeServer server)
            {
                _server = server;
            }

            public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler)
            {
                async ValueTask Handle(ServerConnectionContext ctx)
                {
                    try
                    {
                        TRequest request = ctx.GetUnaryRequest(method.RequestMarshaller);
                        TResponse response = await handler(request, ctx.CallContext).ConfigureAwait(false);
                        await ctx.UnarySuccess(method.ResponseMarshaller, response).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await ctx.UnaryError(ex).ConfigureAwait(false);
                    }
                }

                _server._methodHandlers.Add(method.FullName, Handle);
            }

            public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ClientStreamingServerMethod<TRequest, TResponse> handler)
            {
                async ValueTask Handle(ServerConnectionContext ctx)
                {
                    try
                    {
                        TResponse response = await handler(ctx.GetRequestStreamReader(method.RequestMarshaller), ctx.CallContext).ConfigureAwait(false);
                        ctx.Success(SerializationHelpers.Serialize(method.ResponseMarshaller, response));
                    }
                    catch (Exception ex)
                    {
                        ctx.Error(ex);
                    }
                }

                _server._methodHandlers.Add(method.FullName, Handle);
            }

            public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ServerStreamingServerMethod<TRequest, TResponse> handler)
            {
                async ValueTask Handle(ServerConnectionContext ctx)
                {
                    try
                    {
                        TRequest request = ctx.GetRequest(method.RequestMarshaller);
                        await handler(request, ctx.GetResponseStreamWriter(method.ResponseMarshaller), ctx.CallContext).ConfigureAwait(false);
                        ctx.Success();
                    }
                    catch (Exception ex)
                    {
                        ctx.Error(ex);
                    }
                }

                _server._methodHandlers.Add(method.FullName, Handle);
            }

            public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, DuplexStreamingServerMethod<TRequest, TResponse> handler)
            {
                async ValueTask Handle(ServerConnectionContext ctx)
                {
                    try
                    {
                        await handler(ctx.GetRequestStreamReader(method.RequestMarshaller), ctx.GetResponseStreamWriter(method.ResponseMarshaller), ctx.CallContext).ConfigureAwait(false);
                        ctx.Success();
                    }
                    catch (Exception ex)
                    {
                        ctx.Error(ex);
                    }
                }

                _server._methodHandlers.Add(method.FullName, Handle);
            }
        }
    }
}