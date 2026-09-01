using System.ComponentModel;
using System.Diagnostics;
using AppKeeper.Interop;
using AppKeeper.Models;

namespace AppKeeper.Services;

public sealed record GuardSnapshot(
    GuardedAppDefinition Definition,
    GuardStatus Status,
    int SessionRestartCount,
    int? ProcessId,
    string? LastError);

public sealed class ProcessGuardService : IAsyncDisposable
{
    private readonly ConfigService configService;
    private readonly RestartPolicy restartPolicy = new();
    private readonly Dictionary<Guid, RuntimeGuard> guards = [];
    private readonly object gate = new();
    private AppSettings settings = new();
    private bool isDisposing;

    public ProcessGuardService(ConfigService configService)
    {
        this.configService = configService;
    }

    public event Action<GuardSnapshot>? Changed;
    public event Action<string>? Notice;

    public AppSettings Settings => settings;
    public string ConfigPath => configService.ActivePath;
    public bool IsUsingFallbackConfig => configService.IsUsingFallback;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        settings = await configService.LoadAsync(cancellationToken);
        lock (gate)
        {
            foreach (var definition in settings.Applications)
                guards[definition.Id] = new RuntimeGuard(definition);
        }
    }

    public async Task StartEnabledAsync()
    {
        RuntimeGuard[] candidates;
        lock (gate)
        {
            candidates = guards.Values.Where(x => x.Definition.Enabled && !x.Definition.Paused).ToArray();
        }

        foreach (var guard in candidates)
            await StartOrAttachAsync(guard);
    }

    public IReadOnlyList<GuardSnapshot> GetSnapshots()
    {
        lock (gate)
        {
            return guards.Values.Select(ToSnapshot).OrderBy(x => x.Definition.DisplayName).ToArray();
        }
    }

    public async Task<GuardSnapshot> AddAsync(string executablePath)
    {
        var normalizedPath = NativeProcess.NormalizePath(executablePath);
        if (!File.Exists(normalizedPath) || !string.Equals(Path.GetExtension(normalizedPath), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("请选择一个存在的 EXE 文件。", nameof(executablePath));

        var definition = new GuardedAppDefinition
        {
            ExecutablePath = normalizedPath,
            DisplayName = GetDisplayName(normalizedPath),
            Enabled = true
        };
        var runtimeGuard = new RuntimeGuard(definition);
        lock (gate)
        {
            ThrowIfDisposing();
            if (settings.Applications.Any(x => NativeProcess.PathsEqual(x.ExecutablePath, normalizedPath)))
                throw new InvalidOperationException("这个程序已经在守护列表中。");

            settings.Applications.Add(definition);
            guards.Add(definition.Id, runtimeGuard);
        }

        try
        {
            await SaveAsync();
        }
        catch
        {
            lock (gate)
            {
                runtimeGuard.IsRemoved = true;
                guards.Remove(definition.Id);
                settings.Applications.Remove(definition);
            }

            throw;
        }

        await StartOrAttachAsync(runtimeGuard);
        return ToSnapshot(runtimeGuard);
    }

    public async Task SetEnabledAsync(Guid id, bool enabled)
    {
        RuntimeGuard guard = GetGuard(id);
        await guard.Lifecycle.WaitAsync();
        try
        {
            guard.Definition.Enabled = enabled;
            if (!enabled)
            {
                StopWatching(guard);
                guard.Status = GuardStatus.Stopped;
                guard.LastError = null;
                Publish(guard);
            }
            else if (guard.Definition.Paused)
            {
                guard.Status = GuardStatus.Paused;
                Publish(guard);
            }
            else
            {
                await StartOrAttachCoreAsync(guard);
            }

            await SaveAsync();
        }
        finally
        {
            guard.Lifecycle.Release();
        }
    }

    public async Task ResumeAsync(Guid id)
    {
        var guard = GetGuard(id);
        await guard.Lifecycle.WaitAsync();
        try
        {
            guard.Definition.Paused = false;
            guard.Definition.FailureTimestamps.Clear();
            guard.LastError = null;
            if (guard.Definition.Enabled)
                await StartOrAttachCoreAsync(guard);
            else
            {
                guard.Status = GuardStatus.Stopped;
                Publish(guard);
            }

            await SaveAsync();
        }
        finally
        {
            guard.Lifecycle.Release();
        }
    }

    public async Task RemoveAsync(Guid id)
    {
        var guard = GetGuard(id);
        await guard.Lifecycle.WaitAsync();
        try
        {
            guard.IsRemoved = true;
            StopWatching(guard);
            lock (gate)
            {
                guards.Remove(id);
                settings.Applications.RemoveAll(x => x.Id == id);
            }

            await SaveAsync();
        }
        finally
        {
            guard.Lifecycle.Release();
        }
    }

    public async Task UpdateStartWithWindowsAsync(bool enabled)
    {
        settings.StartWithWindows = enabled;
        await SaveAsync();
    }

    public async ValueTask DisposeAsync()
    {
        RuntimeGuard[] current;
        lock (gate)
        {
            isDisposing = true;
            current = guards.Values.ToArray();
            guards.Clear();
        }

        foreach (var guard in current)
        {
            await guard.Lifecycle.WaitAsync();
            try
            {
                guard.IsRemoved = true;
                StopWatching(guard);
            }
            finally
            {
                guard.Lifecycle.Release();
            }
        }
    }

    private async Task StartOrAttachAsync(RuntimeGuard guard)
    {
        await guard.Lifecycle.WaitAsync();
        try
        {
            await StartOrAttachCoreAsync(guard);
        }
        finally
        {
            guard.Lifecycle.Release();
        }
    }

    private Task StartOrAttachCoreAsync(RuntimeGuard guard)
    {
        if (guard.IsRemoved || !guard.Definition.Enabled || guard.Definition.Paused)
            return Task.CompletedTask;

        StopWatching(guard);
        guard.Status = GuardStatus.Restarting;
        guard.LastError = null;
        Publish(guard);

        try
        {
            var process = FindExistingProcess(guard.Definition.ExecutablePath) ?? StartProcess(guard.Definition.ExecutablePath);
            guard.Process = process;
            guard.ProcessId = process.Id;
            guard.Generation++;
            var generation = guard.Generation;
            var processId = process.Id;
            guard.Wait = ProcessWaitRegistration.Create(process.SafeHandle, () => _ = HandleExitAsync(guard.Definition.Id, processId, generation));
            guard.Status = GuardStatus.Running;
            Publish(guard);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or SystemException)
        {
            guard.Status = GuardStatus.Error;
            guard.LastError = ex.Message;
            Publish(guard);
            Notice?.Invoke($"{guard.Definition.DisplayName} 启动失败：{ex.Message}");
        }

        return Task.CompletedTask;
    }

    private async Task HandleExitAsync(Guid id, int processId, int generation)
    {
        RuntimeGuard? guard;
        lock (gate)
        {
            guards.TryGetValue(id, out guard);
        }

        if (guard is null)
            return;

        await guard.Lifecycle.WaitAsync();
        try
        {
            if (guard.IsRemoved || guard.ProcessId != processId || guard.Generation != generation || guard.Status != GuardStatus.Running)
                return;

            StopWatching(guard);
            var now = DateTimeOffset.UtcNow;
            if (restartPolicy.RegisterFailure(guard.Definition.FailureTimestamps, now))
            {
                guard.Definition.Paused = true;
                guard.Status = GuardStatus.Paused;
                guard.LastError = "5 分钟内连续退出 3 次，已暂停守护。";
                Publish(guard);
                Notice?.Invoke($"{guard.Definition.DisplayName} 已暂停：5 分钟内连续退出 3 次。");
            }
            else if (guard.Definition.Enabled)
            {
                guard.Definition.LifetimeRestartCount++;
                guard.SessionRestartCount++;
                guard.Status = GuardStatus.Restarting;
                Publish(guard);
                await StartOrAttachCoreAsync(guard);
            }

            await SaveAsync();
        }
        finally
        {
            guard.Lifecycle.Release();
        }
    }

    private void StopWatching(RuntimeGuard guard)
    {
        guard.Generation++;
        guard.Wait?.Dispose();
        guard.Wait = null;
        guard.Process?.Dispose();
        guard.Process = null;
        guard.ProcessId = null;
    }

    private RuntimeGuard GetGuard(Guid id)
    {
        lock (gate)
        {
            return guards.TryGetValue(id, out var guard)
                ? guard
                : throw new KeyNotFoundException("找不到指定的守护项目。");
        }
    }

    private void ThrowIfDisposing()
    {
        if (isDisposing)
            throw new ObjectDisposedException(nameof(ProcessGuardService));
    }

    private void Publish(RuntimeGuard guard) => Changed?.Invoke(ToSnapshot(guard));

    private GuardSnapshot ToSnapshot(RuntimeGuard guard) =>
        new(guard.Definition, guard.Status, guard.SessionRestartCount, guard.ProcessId, guard.LastError);

    private Task SaveAsync() => configService.SaveAsync(settings);

    private static Process? FindExistingProcess(string executablePath)
    {
        var name = Path.GetFileNameWithoutExtension(executablePath);
        foreach (var process in Process.GetProcessesByName(name))
        {
            try
            {
                var imagePath = NativeProcess.TryGetImagePath((uint)process.Id);
                if (imagePath is not null && NativeProcess.PathsEqual(imagePath, executablePath))
                    return process;
            }
            catch (InvalidOperationException)
            {
                process.Dispose();
                continue;
            }

            process.Dispose();
        }

        return null;
    }

    private static Process StartProcess(string executablePath)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath),
            UseShellExecute = true
        });
        return process ?? throw new InvalidOperationException("Windows 没有返回新的进程句柄。");
    }

    private static string GetDisplayName(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return !string.IsNullOrWhiteSpace(info.FileDescription)
                ? info.FileDescription
                : !string.IsNullOrWhiteSpace(info.ProductName) ? info.ProductName : Path.GetFileNameWithoutExtension(path);
        }
        catch
        {
            return Path.GetFileNameWithoutExtension(path);
        }
    }

    private sealed class RuntimeGuard(GuardedAppDefinition definition)
    {
        public GuardedAppDefinition Definition { get; } = definition;
        public GuardStatus Status { get; set; } = definition.Paused ? GuardStatus.Paused : GuardStatus.Stopped;
        public int SessionRestartCount { get; set; }
        public int? ProcessId { get; set; }
        public int Generation { get; set; }
        public string? LastError { get; set; }
        public Process? Process { get; set; }
        public ProcessWaitRegistration? Wait { get; set; }
        public bool IsRemoved { get; set; }
        public SemaphoreSlim Lifecycle { get; } = new(1, 1);
    }
}
