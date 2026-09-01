using System.ComponentModel;
using System.Drawing;
using System.Windows;
using AppKeeper.Services;
using AppKeeper.ViewModels;
using AppKeeper.Views;
using Forms = System.Windows.Forms;

namespace AppKeeper;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private readonly Forms.NotifyIcon trayIcon;
    private readonly Action exitApplication;

    public MainWindow(ProcessGuardService guardService, StartupService startupService, Action exitApplication)
    {
        InitializeComponent();
        this.exitApplication = exitApplication;
        viewModel = new MainViewModel(guardService, startupService, AddProgramAsync);
        DataContext = viewModel;

        var trayMenu = new Forms.ContextMenuStrip();
        trayMenu.Items.Add("打开 App Keeper", null, (_, _) => ShowFromTray());
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        trayMenu.Items.Add("退出", null, (_, _) => this.exitApplication());
        trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty) ?? SystemIcons.Application,
            Text = "App Keeper",
            Visible = true,
            ContextMenuStrip = trayMenu
        };
        trayIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    public bool AllowClose { get; set; }

    public void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
    }

    private void WindowLoaded(object sender, RoutedEventArgs e) => PositionAtCorner();

    private void PositionAtCorner()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 16;
        Top = workArea.Bottom - ActualHeight - 16;
    }

    private void WindowDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }

    private void HideClick(object sender, RoutedEventArgs e) => HideToTray();

    public void ShowFromTray()
    {
        ShowInTaskbar = false;
        Show();
        WindowState = WindowState.Normal;
        PositionAtCorner();
        Activate();
    }

    private async Task AddProgramAsync()
    {
        var dialog = new AddExeWindow { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            await viewModel.AddAsync(dialog.SelectedPath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "无法添加程序", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void RemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AppRowViewModel row })
        {
            var result = System.Windows.MessageBox.Show(this, $"确定从守护列表移除“{row.DisplayName}”？\n不会关闭目标程序。", "移除守护", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.OK)
                await viewModel.RemoveAsync(row);
        }
    }

    private async void ResumeClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AppRowViewModel row })
            await viewModel.ResumeAsync(row);
    }

    private async void StartupChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.IsLoaded)
            await viewModel.SetStartupAsync(checkBox.IsChecked == true);
    }

    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (AllowClose)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
            return;
        }

        e.Cancel = true;
        HideToTray();
    }
}
