using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using AppKeeper.Services;
using WpfApplication = System.Windows.Application;

namespace AppKeeper.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ProcessGuardService guardService;
    private readonly StartupService startupService;
    private readonly Func<Task> showAddWindow;
    private string noticeText = string.Empty;

    public MainViewModel(ProcessGuardService guardService, StartupService startupService, Func<Task> showAddWindow)
    {
        this.guardService = guardService;
        this.startupService = startupService;
        this.showAddWindow = showAddWindow;
        AddCommand = new RelayCommand(() => _ = this.showAddWindow());
        guardService.Changed += OnGuardChanged;
        guardService.Notice += OnNotice;
        StartWithWindows = guardService.Settings.StartWithWindows || startupService.IsEnabled();

        foreach (var snapshot in guardService.GetSnapshots())
            Applications.Add(CreateRow(snapshot));
    }

    public ObservableCollection<AppRowViewModel> Applications { get; } = [];
    public RelayCommand AddCommand { get; }
    public bool HasApplications => Applications.Count > 0;
    public string GuardSummary => Applications.Count == 0 ? "还没有守护项目" : $"正在管理 {Applications.Count} 个程序";
    public string ConfigPath => guardService.ConfigPath;
    public string NoticeText
    {
        get => noticeText;
        private set { noticeText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasNotice)); }
    }
    public bool HasNotice => !string.IsNullOrWhiteSpace(NoticeText);
    public bool StartWithWindows { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task AddAsync(string path)
    {
        var snapshot = await guardService.AddAsync(path);
        await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
        {
            Applications.Add(CreateRow(snapshot));
            RefreshSummary();
        });
    }

    public async Task RemoveAsync(AppRowViewModel row)
    {
        await guardService.RemoveAsync(row.Id);
        await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
        {
            Applications.Remove(row);
            RefreshSummary();
        });
    }

    public async Task ResumeAsync(AppRowViewModel row) => await row.ResumeAsync();

    public async Task SetStartupAsync(bool enabled)
    {
        try
        {
            startupService.SetEnabled(enabled);
            await guardService.UpdateStartWithWindowsAsync(enabled);
            StartWithWindows = enabled;
            OnPropertyChanged(nameof(StartWithWindows));
        }
        catch (Exception ex)
        {
            NoticeText = $"开机启动设置失败：{ex.Message}";
            StartWithWindows = !enabled;
            OnPropertyChanged(nameof(StartWithWindows));
        }
    }

    private AppRowViewModel CreateRow(GuardSnapshot snapshot) => new(
        snapshot,
        enabled => guardService.SetEnabledAsync(snapshot.Definition.Id, enabled),
        () => guardService.ResumeAsync(snapshot.Definition.Id));

    private void OnGuardChanged(GuardSnapshot snapshot)
    {
        _ = WpfApplication.Current.Dispatcher.InvokeAsync(() =>
        {
            var row = Applications.FirstOrDefault(x => x.Id == snapshot.Definition.Id);
            if (row is not null)
                row.Apply(snapshot);
            else if (guardService.GetSnapshots().Any(x => x.Definition.Id == snapshot.Definition.Id))
                Applications.Add(CreateRow(snapshot));
            RefreshSummary();
        });
    }

    private void OnNotice(string notice) => WpfApplication.Current.Dispatcher.Invoke(() => NoticeText = notice);

    private void RefreshSummary()
    {
        OnPropertyChanged(nameof(HasApplications));
        OnPropertyChanged(nameof(GuardSummary));
        OnPropertyChanged(nameof(ConfigPath));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
