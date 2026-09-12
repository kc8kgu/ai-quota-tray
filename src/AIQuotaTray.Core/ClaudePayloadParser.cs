using System.Text.Json;

namespace AIQuotaTray.Core;

public static class ClaudePayloadParser
{
    public static ProviderSnapshot Parse(string json, DateTimeOffset observedAt)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var windows = new List<UsageWindow>();

        if (root.TryGetProperty("rate_limits", out var limits))
        {
            AddWindow(limits, "five_hour", "5h", windows);
            AddWindow(limits, "seven_day", "7d", windows);
        }

        return windows.Count == 0
            ? new ProviderSnapshot(ProviderKind.Claude, ProviderHealth.NotConfigured,
                "Waiting for the first Claude Code response", observedAt, [])
            : new ProviderSnapshot(ProviderKind.Claude, ProviderHealth.Available,
                "Connected through Claude Code", observedAt, windows);
    }

    private static void AddWindow(JsonElement limits, string propertyName, string label, List<UsageWindow> windows)
    {
        if (!limits.TryGetProperty(propertyName, out var window) ||
            !window.TryGetProperty("used_percentage", out var used) ||
            !used.TryGetDouble(out var usedPercent))
        {
            return;
        }

        DateTimeOffset? resetsAt = null;
        if (window.TryGetProperty("resets_at", out var reset) && reset.TryGetInt64(out var epoch))
        {
            resetsAt = DateTimeOffset.FromUnixTimeSeconds(epoch);
        }

        windows.Add(new UsageWindow(propertyName, label, usedPercent, resetsAt));
    }
}

