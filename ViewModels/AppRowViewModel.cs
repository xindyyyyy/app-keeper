using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using AppKeeper.Models;
using AppKeeper.Services;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace AppKeeper.ViewModels;

public sealed class AppRowViewModel : INotifyPropertyChanged
{
    private readonly Func<bool, Task> toggle;
    private readonly Func<Task> resume;
    private bool suppressToggle;
    private bool isGuardEnabled;
    private GuardStatus status;
    private int sessionRestartCount;
    private int lifetimeRestartCount;
    private int? processId;
    private string? lastError;

    public AppRowViewModel(GuardSnapshot snapshot, Func<bool, Task> toggle, Func<Task> resume)
    {
        Definition = snapshot.Definition;
        this.toggle = toggle;
        this.resume = resume;
        Icon = IconService.GetIcon(Definition.ExecutablePath);
        Apply(snapshot);
    }

    public GuardedAppDefinition Definition { get; }
    public Guid Id => Definition.Id;
    public string DisplayName => string.IsNullOrWhiteSpace(Definition.DisplayName)
        ? Path.GetFileNameWithoutExtension(Definition.ExecutablePath)
        : Definition.DisplayName;
    public string ExecutablePath => Definition.ExecutablePath;
    public ImageSource? Icon { get; }
    public bool IsPaused => status == GuardStatus.Paused;
    public bool IsGuardEnabled
    {
        get => isGuardEnabled;
        set
        {
            if (isGuardEnabled == value)
                return;
            isGuardEnabled = value;
            OnPropertyChanged();
            if (!suppressToggle)
                _ = toggle(value);
        }
    }
    public string StatusText => status switch
    {
        GuardStatus.Running => "运行中",
        GuardStatus.Restarting => "正在重启",
        GuardStatus.Paused => "已暂停",
        GuardStatus.Error => "启动失败",
        _ => "已停止"
    };
    public MediaBrush StatusBrush => status switch
    {
        GuardStatus.Running => System.Windows.Application.Current.Resources["SuccessBrush"] as MediaBrush ?? MediaBrushes.Green,
        GuardStatus.Restarting => System.Windows.Application.Current.Resources["WarningBrush"] as MediaBrush ?? MediaBrushes.Orange,
        GuardStatus.Paused or GuardStatus.Error => System.Windows.Application.Current.Resources["DangerBrush"] as MediaBrush ?? MediaBrushes.Red,
        _ => System.Windows.Application.Current.Resources["MutedTextBrush"] as MediaBrush ?? MediaBrushes.Gray
    };
    public string RestartCountText => $"本次 {sessionRestartCount}  ·  累计 {lifetimeRestartCount}";
    public string ProcessText => processId is int pid ? $"PID {pid}" : string.Empty;
    public string ErrorText => lastError ?? string.Empty;
    public bool HasError => !string.IsNullOrWhiteSpace(lastError);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Apply(GuardSnapshot snapshot)
    {
        suppressToggle = true;
        try
        {
            status = snapshot.Status;
            isGuardEnabled = snapshot.Definition.Enabled;
            sessionRestartCount = snapshot.SessionRestartCount;
            lifetimeRestartCount = snapshot.Definition.LifetimeRestartCount;
            processId = snapshot.ProcessId;
            lastError = snapshot.LastError;
        }
        finally
        {
            suppressToggle = false;
        }

        OnPropertyChanged(string.Empty);
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsGuardEnabled));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(RestartCountText));
        OnPropertyChanged(nameof(ProcessText));
        OnPropertyChanged(nameof(ErrorText));
        OnPropertyChanged(nameof(HasError));
    }

    public Task ResumeAsync() => resume();

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
