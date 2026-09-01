using System.Threading;
using System.Windows;
using AppKeeper.Services;

using WpfApplication = System.Windows.Application;

namespace AppKeeper;

public partial class App : WpfApplication
{
    private Mutex? singleInstanceMutex;
    private bool ownsSingleInstanceMutex;
    private EventWaitHandle? activationEvent;
    private RegisteredWaitHandle? activationRegistration;
    private MainWindow? mainWindow;
    private ProcessGuardService? guardService;

    public async void OnStartup(object sender, StartupEventArgs e)
    {
        singleInstanceMutex = new Mutex(true, @"Local\AppKeeper.SingleInstance", out var isFirstInstance);
        ownsSingleInstanceMutex = isFirstInstance;
        if (!isFirstInstance)
        {
            try
            {
                using var existingEvent = EventWaitHandle.OpenExisting(@"Local\AppKeeper.Activate");
                existingEvent.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The first instance may be between startup stages.
            }

            Shutdown();
            return;
        }

        activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\AppKeeper.Activate");
        activationRegistration = ThreadPool.RegisterWaitForSingleObject(activationEvent, (_, _) =>
        {
            Dispatcher.BeginInvoke(ShowMainWindow);
        }, null, Timeout.Infinite, true);

        var configService = new ConfigService();
        guardService = new ProcessGuardService(configService);
        await guardService.LoadAsync();

        mainWindow = new MainWindow(guardService, new StartupService(), () => ShutdownFromTray());
        mainWindow.Show();
        if (e.Args.Any(x => string.Equals(x, "--minimized", StringComparison.OrdinalIgnoreCase)))
            mainWindow.HideToTray();

        await guardService.StartEnabledAsync();
    }

    private void ShowMainWindow()
    {
        if (mainWindow is null)
            return;

        mainWindow.ShowFromTray();
    }

    private async void ShutdownFromTray()
    {
        if (mainWindow is not null)
            mainWindow.AllowClose = true;

        if (guardService is not null)
            await guardService.DisposeAsync();

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        activationRegistration?.Unregister(null);
        activationEvent?.Dispose();
        if (ownsSingleInstanceMutex)
            singleInstanceMutex?.ReleaseMutex();
        singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
