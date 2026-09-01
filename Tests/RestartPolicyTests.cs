using System.Diagnostics;
using AppKeeper.Services;

namespace AppKeeper.Tests;

public sealed class RestartPolicyTests
{
    [Fact]
    public async Task ProcessWaitRegistrationFiresWhenProcessExits()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c exit 0",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        Assert.NotNull(process);

        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = ProcessWaitRegistration.Create(process!.SafeHandle, () => exited.TrySetResult());

        var completed = await Task.WhenAny(exited.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(exited.Task, completed);
    }

    [Fact]
    public void PausesOnThirdFailureInsideFiveMinutes()
    {
        var policy = new RestartPolicy();
        var failures = new List<DateTimeOffset>();
        var start = DateTimeOffset.UtcNow;

        Assert.False(policy.RegisterFailure(failures, start));
        Assert.False(policy.RegisterFailure(failures, start.AddMinutes(1)));
        Assert.True(policy.RegisterFailure(failures, start.AddMinutes(2)));
        Assert.Equal(3, failures.Count);
    }

    [Fact]
    public void DropsFailuresOutsideTheRollingWindow()
    {
        var policy = new RestartPolicy();
        var failures = new List<DateTimeOffset>();
        var start = DateTimeOffset.UtcNow;

        policy.RegisterFailure(failures, start);
        policy.RegisterFailure(failures, start.AddMinutes(1));

        Assert.False(policy.RegisterFailure(failures, start.AddMinutes(5).AddSeconds(1)));
        Assert.Equal(2, failures.Count);
    }

    [Fact]
    public void ResetClearsFailures()
    {
        var policy = new RestartPolicy();
        var failures = new List<DateTimeOffset> { DateTimeOffset.UtcNow };

        policy.Reset(failures);

        Assert.Empty(failures);
    }
}
