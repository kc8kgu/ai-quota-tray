namespace AIQuotaTray.Core;

public static class SnapshotRules
{
    public static bool ShouldReplace(ProviderSnapshot? existing, ProviderSnapshot incoming) =>
        existing is null || incoming.ObservedAt >= existing.ObservedAt;

    public static ProviderHealth EffectiveHealth(ProviderSnapshot snapshot, DateTimeOffset now)
    {
        if (snapshot.Health != ProviderHealth.Available || snapshot.Windows.Count == 0)
        {
            return snapshot.Health;
        }

        return snapshot.Windows.Any(window => window.ResetsAt is not null && window.ResetsAt <= now)
            ? ProviderHealth.Stale
            : ProviderHealth.Available;
    }

    public static string DurationLabel(long? minutes, string fallback)
    {
        if (minutes is null or <= 0)
        {
            return fallback;
        }

        if (minutes.Value % (60 * 24 * 7) == 0)
        {
            return $"{minutes.Value / (60 * 24 * 7)}w";
        }

        if (minutes.Value % (60 * 24) == 0)
        {
            return $"{minutes.Value / (60 * 24)}d";
        }

        if (minutes.Value % 60 == 0)
        {
            return $"{minutes.Value / 60}h";
        }

        return $"{minutes.Value}m";
    }
}
