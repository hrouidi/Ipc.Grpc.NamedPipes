﻿
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Ipc.Grpc.NamedPipes.Tests.Server;

public class IdleTimeInterceptor : Interceptor
{
    private readonly Timer _timer;
    private readonly Action _ileTimeAction;

    public IdleTimeInterceptor(TimeSpan idleDuration, Action ileTimeAction)
    {
        _ileTimeAction = ileTimeAction;
        _timer = new Timer(idleDuration.TotalMilliseconds);
        _timer.Elapsed += OnTimerElapsed;
    }

    public void Start() => _timer.Start();

    private void OnTimerElapsed(object sender, ElapsedEventArgs e)
    {
        _ileTimeAction?.Invoke();
        _timer.Elapsed -= OnTimerElapsed;
    }

    private void Reset()
    {
        _timer.Stop();
        _timer.Start();
    }

    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        Reset();
        return base.UnaryServerHandler(request, context, continuation);

    }

    public override Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Reset();
        return base.ClientStreamingServerHandler(requestStream, context, continuation);
    }

    public override Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Reset();
        return base.ServerStreamingServerHandler(request, responseStream, context, continuation);
    }

    public override Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Reset();
        return base.DuplexStreamingServerHandler(requestStream, responseStream, context, continuation);
    }

}