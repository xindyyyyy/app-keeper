namespace AppKeeper.Models;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public bool StartWithWindows { get; set; }
    public List<GuardedAppDefinition> Applications { get; set; } = [];
}
