using System.Text.Json;

namespace AIQuotaTray.Core;

public static class CodexPayloadParser
{
    public static ProviderSnapshot Parse(JsonElement result, DateTimeOffset observedAt)
    {
        var windows = new List<UsageWindow>();

        if (result.TryGetProperty("rateLimitsByLimitId", out var byId) && byId.ValueKind == JsonValueKind.Object)
        {
            foreach (var bucket in byId.EnumerateObject())
            {
                AddSnapshot(bucket.Value, bucket.Name, windows);
            }
        }

        if (windows.Count == 0 && result.TryGetProperty("rateLimits", out var legacy))
        {
            AddSnapshot(legacy, "codex", windows);
        }

        return windows.Count == 0
            ? new ProviderSnapshot(ProviderKind.Codex, ProviderHealth.Error,
                "No subscription usage windows were returned", observedAt, [])
            : new ProviderSnapshot(ProviderKind.Codex, ProviderHealth.Available,
                "Connected through Codex App Server", observedAt, windows);
    }

    private static void AddSnapshot(JsonElement snapshot, string bucketId, List<UsageWindow> windows)
    {
        var bucketName = ReadString(snapshot, "limitName") ?? bucketId;
        AddWindow(snapshot, "primary", $"{bucketName} primary", $"{bucketId}:primary", windows);
        AddWindow(snapshot, "secondary", $"{bucketName} secondary", $"{bucketId}:secondary", windows);
    }

    private static void AddWindow(JsonElement snapshot, string propertyName, string fallbackLabel,
        string id, List<UsageWindow> windows)
    {
        if (!snapshot.TryGetProperty(propertyName, out var window) || window.ValueKind != JsonValueKind.Object ||
            !window.TryGetProperty("usedPercent", out var used) || !used.TryGetDouble(out var usedPercent))
        {
            return;
        }

        long? minutes = window.TryGetProperty("windowDurationMins", out var duration) && duration.TryGetInt64(out var value)
            ? value
            : null;
        DateTimeOffset? resetsAt = window.TryGetProperty("resetsAt", out var reset) && reset.TryGetInt64(out var epoch)
            ? DateTimeOffset.FromUnixTimeSeconds(epoch)
            : null;
        var durationLabel = SnapshotRules.DurationLabel(minutes, fallbackLabel);
        windows.Add(new UsageWindow(id, durationLabel, usedPercent, resetsAt));
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
