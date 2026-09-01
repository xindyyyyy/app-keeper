namespace AppKeeper.Models;

public sealed class GuardedAppDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExecutablePath { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool Paused { get; set; }
    public int LifetimeRestartCount { get; set; }
    public List<DateTimeOffset> FailureTimestamps { get; set; } = [];
}
