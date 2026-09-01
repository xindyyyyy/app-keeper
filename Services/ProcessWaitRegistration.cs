using Microsoft.Win32.SafeHandles;

namespace AppKeeper.Services;

public sealed class ProcessWaitRegistration : IDisposable
{
    private RegisteredWaitHandle? registration;
    private readonly WaitHandle waitHandle;

    private ProcessWaitRegistration(WaitHandle waitHandle, RegisteredWaitHandle registration)
    {
        this.waitHandle = waitHandle;
        this.registration = registration;
    }

    public static ProcessWaitRegistration Create(SafeProcessHandle processHandle, Action callback)
    {
        var waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset)
        {
            SafeWaitHandle = new SafeWaitHandle(processHandle.DangerousGetHandle(), ownsHandle: false)
        };
        var registration = ThreadPool.RegisterWaitForSingleObject(
            waitHandle,
            static (state, timedOut) =>
            {
                if (!timedOut && state is Action action)
                    action();
            },
            callback,
            Timeout.Infinite,
            executeOnlyOnce: true);
        return new ProcessWaitRegistration(waitHandle, registration);
    }

    public void Dispose()
    {
        registration?.Unregister(null);
        registration = null;
        waitHandle.Dispose();
    }
}
