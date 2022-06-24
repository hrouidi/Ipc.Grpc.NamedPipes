using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Sources;

namespace Ipc.Grpc.SharedMemory;

public static class WaitHandleExtensions
{
    public static async ValueTask<bool> WaitAsync(this WaitHandle handle, TimeSpan timeout, CancellationToken token = default)
    {
        //ManualResetValueTaskSourceCore<bool> tcs = new ManualResetValueTaskSourceCore<bool>();
        //ManualResetValueTaskSource<bool> tcs = new();
        //ValueTask<bool> ret = new(tcs, 0);
        var tcs = new TaskCompletionSource<bool>();
        using (new ThreadPoolRegistration(handle, timeout, tcs))
        await using (token.Register(static state => (((TaskCompletionSource<bool>)state)).TrySetCanceled(), tcs, useSynchronizationContext: false))
            return await tcs.Task.ConfigureAwait(false);
    }

    private readonly struct ThreadPoolRegistration : IDisposable
    {
        private readonly RegisteredWaitHandle _registeredWaitHandle;

        public ThreadPoolRegistration(WaitHandle handle, TimeSpan timeout, TaskCompletionSource<bool> tcs)
        {
            _registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(handle,
                static (state, timedOut) => ((TaskCompletionSource<bool>)state).TrySetResult(!timedOut), tcs, timeout, executeOnlyOnce: true);
        }

        void IDisposable.Dispose() => _registeredWaitHandle.Unregister(null);
    }

    public struct ManualResetValueTaskSource<T> : IValueTaskSource<T>, IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<T> _logic; // mutable struct; do not make this readonly

        public bool RunContinuationsAsynchronously
        {
            get => _logic.RunContinuationsAsynchronously;
            set => _logic.RunContinuationsAsynchronously = value;
        }

        public void Reset() => _logic.Reset();
        public void SetResult(T result) => _logic.SetResult(result);
        public void SetException(Exception error) => _logic.SetException(error);

        void IValueTaskSource.GetResult(short token) => _logic.GetResult(token);
        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) => _logic.GetStatus(token);

        void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token,
            ValueTaskSourceOnCompletedFlags flags) => _logic.OnCompleted(continuation, state, token, flags);

        T IValueTaskSource<T>.GetResult(short token) => _logic.GetResult(token);
        ValueTaskSourceStatus IValueTaskSource<T>.GetStatus(short token) => _logic.GetStatus(token);

        void IValueTaskSource<T>.OnCompleted(Action<object?> continuation, object? state, short token,
            ValueTaskSourceOnCompletedFlags flags) => _logic.OnCompleted(continuation, state, token, flags);
    }
}