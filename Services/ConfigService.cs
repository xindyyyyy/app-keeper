using System.Text.Json;
using System.Text.Json.Serialization;
using AppKeeper.Models;

namespace AppKeeper.Services;

public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string primaryPath;
    private readonly string fallbackPath;
    private readonly SemaphoreSlim saveLock = new(1, 1);

    public ConfigService()
    {
        primaryPath = Path.Combine(AppContext.BaseDirectory, "appkeeper.settings.json");
        fallbackPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "App Keeper",
            "appkeeper.settings.json");
    }

    public string ActivePath { get; private set; } = string.Empty;
    public bool IsUsingFallback { get; private set; }

    public virtual async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        foreach (var path in PathsToTry())
        {
            if (!File.Exists(path))
                continue;

            try
            {
                await using var stream = File.OpenRead(path);
                var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);
                ActivePath = path;
                IsUsingFallback = string.Equals(path, fallbackPath, StringComparison.OrdinalIgnoreCase);
                return Normalize(settings ?? new AppSettings());
            }
            catch (JsonException)
            {
                // Preserve the broken file and let the app start with an empty, usable configuration.
                ActivePath = path;
                IsUsingFallback = string.Equals(path, fallbackPath, StringComparison.OrdinalIgnoreCase);
                return new AppSettings();
            }
            catch (IOException)
            {
                // Try the fallback location below.
            }
            catch (UnauthorizedAccessException)
            {
                // Try the fallback location below.
            }
        }

        ActivePath = primaryPath;
        IsUsingFallback = false;
        return new AppSettings();
    }

    public virtual async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await saveLock.WaitAsync(cancellationToken);
        try
        {
            var target = ActivePath;
            if (string.IsNullOrWhiteSpace(target))
                target = primaryPath;

            try
            {
                await WriteAtomicallyAsync(target, settings, cancellationToken);
                ActivePath = target;
                IsUsingFallback = string.Equals(target, fallbackPath, StringComparison.OrdinalIgnoreCase);
            }
            catch (IOException) when (string.Equals(target, primaryPath, StringComparison.OrdinalIgnoreCase))
            {
                await WriteAtomicallyAsync(fallbackPath, settings, cancellationToken);
                ActivePath = fallbackPath;
                IsUsingFallback = true;
            }
            catch (UnauthorizedAccessException) when (string.Equals(target, primaryPath, StringComparison.OrdinalIgnoreCase))
            {
                await WriteAtomicallyAsync(fallbackPath, settings, cancellationToken);
                ActivePath = fallbackPath;
                IsUsingFallback = true;
            }
        }
        finally
        {
            saveLock.Release();
        }
    }

    private static async Task WriteAtomicallyAsync(string path, AppSettings settings, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var tempPath = path + ".tmp";
        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(tempPath, path, true);
    }

    private IEnumerable<string> PathsToTry()
    {
        yield return primaryPath;
        if (!string.Equals(primaryPath, fallbackPath, StringComparison.OrdinalIgnoreCase))
            yield return fallbackPath;
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.SchemaVersion = 1;
        settings.Applications ??= [];
        foreach (var app in settings.Applications)
        {
            app.FailureTimestamps ??= [];
            app.DisplayName ??= string.Empty;
            app.ExecutablePath ??= string.Empty;
        }

        return settings;
    }
}
