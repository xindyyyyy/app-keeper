using System.Text.Json;
using AppKeeper.Models;
using AppKeeper.Services;

namespace AppKeeper.Tests;

public sealed class ConfigServiceTests
{
    [Fact]
    public async Task DefaultSettingsAreUsable()
    {
        var service = new ConfigService();
        var settings = await service.LoadAsync();

        Assert.Equal(1, settings.SchemaVersion);
        Assert.NotNull(settings.Applications);
    }

    [Fact]
    public void SettingsRoundTripShapeContainsBothCounters()
    {
        var settings = new AppSettings
        {
            Applications =
            [
                new GuardedAppDefinition
                {
                    ExecutablePath = @"C:\Tools\DockLens.exe",
                    DisplayName = "DockLens",
                    LifetimeRestartCount = 7,
                    Paused = true
                }
            ]
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(7, restored!.Applications[0].LifetimeRestartCount);
        Assert.True(restored.Applications[0].Paused);
    }

    [Fact]
    public async Task FailedAddDoesNotLeaveAnInMemoryGuard()
    {
        var service = new ProcessGuardService(new FailingConfigService());
        await service.LoadAsync();

        await Assert.ThrowsAsync<IOException>(() => service.AddAsync(Environment.ProcessPath!));

        Assert.Empty(service.Settings.Applications);
        Assert.Empty(service.GetSnapshots());
    }

    private sealed class FailingConfigService : ConfigService
    {
        public override Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppSettings());

        public override Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            Task.FromException(new IOException("simulated persistence failure"));
    }
}
