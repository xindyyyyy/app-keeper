namespace AppKeeper.Services;

public sealed class RestartPolicy
{
    public const int MaximumFailures = 3;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    public bool RegisterFailure(IList<DateTimeOffset> failures, DateTimeOffset now)
    {
        for (var index = failures.Count - 1; index >= 0; index--)
        {
            if (now - failures[index] > Window)
                failures.RemoveAt(index);
        }

        failures.Add(now);
        return failures.Count >= MaximumFailures;
    }

    public void Reset(ICollection<DateTimeOffset> failures) => failures.Clear();
}
