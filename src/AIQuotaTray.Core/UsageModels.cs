namespace AIQuotaTray.Core;

public enum ProviderKind
{
    Codex,
    Claude
}

public enum ProviderHealth
{
    Connecting,
    Available,
    Stale,
    SignInRequired,
    NotConfigured,
    CliNotFound,
    Error
}

public sealed record UsageWindow(
    string Id,
    string Label,
    double UsedPercent,
    DateTimeOffset? ResetsAt)
{
    public double RemainingPercent => Math.Clamp(100d - UsedPercent, 0d, 100d);
}

public sealed record ProviderSnapshot(
    ProviderKind Provider,
    ProviderHealth Health,
    string StatusMessage,
    DateTimeOffset ObservedAt,
    IReadOnlyList<UsageWindow> Windows)
{
    public static ProviderSnapshot Empty(ProviderKind provider, ProviderHealth health, string message) =>
        new(provider, health, message, DateTimeOffset.UtcNow, []);
}

public sealed record ApplicationSnapshot(ProviderSnapshot Codex, ProviderSnapshot Claude)
{
    public IEnumerable<UsageWindow> TrustworthyWindows(DateTimeOffset now) =>
        new[] { Codex, Claude }
            .Where(snapshot => SnapshotRules.EffectiveHealth(snapshot, now) == ProviderHealth.Available)
            .SelectMany(snapshot => snapshot.Windows);

    public double? LowestRemaining(DateTimeOffset now)
    {
        var values = TrustworthyWindows(now).Select(window => window.RemainingPercent).ToArray();
        return values.Length == 0 ? null : values.Min();
    }
}

