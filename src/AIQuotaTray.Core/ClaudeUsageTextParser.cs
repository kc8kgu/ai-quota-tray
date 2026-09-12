using System.Globalization;
using System.Text.RegularExpressions;

namespace AIQuotaTray.Core;

/// Reads the plain-text report that `claude -p "/usage"` prints. The text is human-facing copy rather
/// than a contract, so every field degrades on its own: an unrecognised limit row is skipped, and a
/// reset stamp that will not parse leaves the window in place with no reset rather than dropping it.
public static partial class ClaudeUsageTextParser
{
    public static ProviderSnapshot Parse(string report, DateTimeOffset observedAt)
    {
        var windows = new List<UsageWindow>();
        foreach (var line in report.Split('\n'))
        {
            var match = LimitLine().Match(line.Trim());
            if (!match.Success) continue;
            if (!double.TryParse(match.Groups["percent"].ValueSpan, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var usedPercent))
            {
                continue;
            }

            var name = match.Groups["name"].Value.Trim();
            var reset = match.Groups["reset"];
            windows.Add(new UsageWindow(
                IdFor(name),
                LabelFor(name),
                usedPercent,
                reset.Success ? ParseReset(reset.Value.Trim(), observedAt) : null));
        }

        return windows.Count == 0
            ? new ProviderSnapshot(ProviderKind.Claude, ProviderHealth.NotConfigured,
                "Claude Code reported no usage limits", observedAt, [])
            : new ProviderSnapshot(ProviderKind.Claude, ProviderHealth.Available,
                "Polled from Claude Code", observedAt, windows);
    }

    /// The year is absent from the report, so it is inferred as the nearest one that does not put the
    /// reset well into the past — which is what carries a late-December reset into the next year.
    private static DateTimeOffset? ParseReset(string text, DateTimeOffset observedAt)
    {
        var match = ResetStamp().Match(text);
        if (!match.Success) return null;

        // No month abbreviation contains "am" or "pm", so casing the designator is safe here.
        var when = match.Groups["when"].Value.Replace("am", "AM").Replace("pm", "PM");
        string[] formats = ["MMM d, h:mmtt", "MMM d, htt", "MMM d, h:mm tt", "MMM d, h tt"];
        if (!DateTime.TryParseExact(when, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return null;
        }

        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(match.Groups["zone"].Value);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }

        if (local < observedAt.UtcDateTime.AddDays(-2)) local = local.AddYears(1);

        try
        {
            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
        }
        catch (ArgumentException)
        {
            // A wall-clock time that a DST transition skipped or repeated cannot be pinned to an instant.
            return null;
        }
    }

    private static string IdFor(string name) => name switch
    {
        "Current session" => "five_hour",
        "Current week (all models)" => "seven_day",
        _ => Slug(name)
    };

    private static string LabelFor(string name) => name switch
    {
        "Current session" => "5h",
        "Current week (all models)" => "7d",
        _ => WeeklyModel().Match(name) is { Success: true } weekly ? $"7d {weekly.Groups["model"].Value}" : name
    };

    private static string Slug(string name)
    {
        var characters = name.ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '_')
            .ToArray();
        return new string(characters).Trim('_');
    }

    [GeneratedRegex(@"^(?<name>[^:]+):\s*(?<percent>\d+(?:\.\d+)?)\s*%\s*used(?:\W+resets\s+(?<reset>.+))?$",
        RegexOptions.IgnoreCase)]
    private static partial Regex LimitLine();

    [GeneratedRegex(@"^(?<when>.+?)\s*\((?<zone>[^)]+)\)$")]
    private static partial Regex ResetStamp();

    [GeneratedRegex(@"^Current week \((?<model>.+)\)$", RegexOptions.IgnoreCase)]
    private static partial Regex WeeklyModel();
}
