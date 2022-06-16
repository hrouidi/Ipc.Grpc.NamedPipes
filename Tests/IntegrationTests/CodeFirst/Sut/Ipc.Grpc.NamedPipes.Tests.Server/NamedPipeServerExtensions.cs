using System;
using Grpc.Core;
using ProtoBuf.Grpc.Configuration;

namespace Ipc.Grpc.NamedPipes.Tests.Server
{
    //TODO : to replace by AddCodeFirst method when it will be available next release of Protobuf-net.grpc package
    public static class NamedPipeServerExtensions
    {
        public static void Bind<TService>(this NamedPipeServer namedPipeServer, TService service) where TService : class
        {
            new NamedPipeServerBinder().Bind<TService>(namedPipeServer, BinderConfiguration.Default, service);
        }

        private class NamedPipeServerBinder : ServerBinder
        {
            private static ServiceBinderBase GetServiceBinderBase(object state)
            {
                return state switch
                {
                    NamedPipeServer server => server.ServiceBinder,
                    _=> throw new NotImplementedException(),
                };
            }
            
            protected override bool TryBind<TService, TRequest, TResponse>(ServiceBindContext bindContext, Method<TRequest, TResponse> method, MethodStub<TService> stub)
            {
                ServiceBinderBase serviceBinder = GetServiceBinderBase(bindContext.State);
                switch (method.Type)
                {
                    case MethodType.Unary:
                        serviceBinder.AddMethod(method, stub.CreateDelegate<UnaryServerMethod<TRequest, TResponse>>());
                        break;
                    case MethodType.ClientStreaming:
                        serviceBinder.AddMethod(method, stub.CreateDelegate<ClientStreamingServerMethod<TRequest, TResponse>>());
                        break;
                    case MethodType.ServerStreaming:
                        serviceBinder.AddMethod(method, stub.CreateDelegate<ServerStreamingServerMethod<TRequest, TResponse>>());
                        break;
                    case MethodType.DuplexStreaming:
                        serviceBinder.AddMethod(method, stub.CreateDelegate<DuplexStreamingServerMethod<TRequest, TResponse>>());
                        break;
                    default:
                        return false;
                }

                return true;
            }
        }
    }
}