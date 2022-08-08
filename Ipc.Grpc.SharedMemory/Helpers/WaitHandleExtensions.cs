using System.Threading.Tasks.Sources;

namespace Ipc.Grpc.SharedMemory.Helpers;

public static class WaitHandleExtensions
{

    public static async ValueTask WaitAsync(this WaitHandle handle, CancellationToken token = default)
    {
        var tcs = new TaskCompletionSource<bool>();
        using (new ThreadPoolRegistration(handle, tcs))
        using (token.Register(OnCancellationTokenCanceled, tcs, useSynchronizationContext: false))
            await tcs.Task.ConfigureAwait(false);
    }

    //public static async ValueTask<bool> WaitAsync(this WaitHandle handle, TimeSpan timeout, CancellationToken token = default)
    //{
    //    var tcs = new TaskCompletionSource<bool>();
    //    using (new ThreadPoolRegistration(handle, timeout, tcs))
    //    await using (token.Register(OnCancellationTokenCanceled, tcs, useSynchronizationContext: false))
    //        return await tcs.Task.ConfigureAwait(false);
    //}

    private static void OnCancellationTokenCanceled(object? state)
    {
        ((TaskCompletionSource<bool>)state!).TrySetCanceled();
    }

    private readonly struct ThreadPoolRegistration : IDisposable
    {
        private readonly RegisteredWaitHandle _registeredWaitHandle;

        public ThreadPoolRegistration(WaitHandle handle, TimeSpan timeout, TaskCompletionSource<bool> tcs)
        {
            _registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(handle, WaitOrTimerCallback, tcs, timeout, executeOnlyOnce: true);
        }
        public ThreadPoolRegistration(WaitHandle handle, TaskCompletionSource<bool> tcs)
        {
            _registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(handle, WaitOrTimerCallback, tcs, -1, executeOnlyOnce: true);
        }

        void IDisposable.Dispose() => _registeredWaitHandle.Unregister(null);

        private static void WaitOrTimerCallback(object? state, bool timedOut)
        {
            ((TaskCompletionSource<bool>)state!).TrySetResult(timedOut == false);
        }
    }
}