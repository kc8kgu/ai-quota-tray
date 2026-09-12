namespace AIQuotaTray.Core;

public static class UsageFormatting
{
    public static string Percent(double value) => $"{Math.Round(value):0}%";

    public static string RelativeAge(DateTimeOffset observedAt, DateTimeOffset now)
    {
        var age = now - observedAt;
        if (age < TimeSpan.Zero || age < TimeSpan.FromMinutes(1)) return "just now";
        if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes}m ago";
        if (age < TimeSpan.FromDays(1)) return $"{(int)age.TotalHours}h ago";
        return $"{(int)age.TotalDays}d ago";
    }

    public static string ResetText(DateTimeOffset? resetsAt, DateTimeOffset now)
    {
        if (resetsAt is null) return "Reset time unavailable";
        if (resetsAt <= now) return "Reset passed; refresh required";

        var remaining = resetsAt.Value - now;
        var relative = remaining.TotalDays >= 1
            ? $"in {(int)remaining.TotalDays}d {remaining.Hours}h"
            : remaining.TotalHours >= 1
                ? $"in {(int)remaining.TotalHours}h {remaining.Minutes}m"
                : $"in {Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))}m";
        return $"Resets {relative} ({resetsAt.Value.ToLocalTime():g})";
    }
}

