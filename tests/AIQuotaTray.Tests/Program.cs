using System.Text.Json;
using System.Reflection;
using System.Security.Cryptography;
using AIQuotaTray.Core;

const string UsageReport = """
    You are currently using your subscription to power your Claude Code usage

    Current session: 31% used · resets Sep 12, 1:20am (America/New_York)
    Current week (all models): 5% used · resets Sep 17, 2am (America/New_York)

    What's contributing to your limits usage?
    Approximate, based on local sessions on this machine — does not include other devices or claude.ai.

    Last 24h · 192 requests · 9 sessions
      Top skills: /run 20%, /code-review 2%
    """;

var tests = new (string Name, Action Run)[]
{
    ("Claude parses subscription windows", ClaudeParsesWindows),
    ("Claude handles missing limits", ClaudeHandlesMissingLimits),
    ("Codex prefers multi-bucket limits", CodexParsesBuckets),
    ("Expired window makes snapshot stale", ExpiredWindowIsStale),
    ("Duration labels are compact", DurationLabelsAreCompact),
    ("Lowest remaining ignores stale providers", LowestRemainingIgnoresStale),
    ("Newest Claude observation wins", NewestObservationWins),
    ("Usage report parses both limit rows", UsageReportParsesRows),
    ("Usage report resolves reset to UTC", UsageReportResolvesReset),
    ("Usage report keeps a row with an unreadable reset", UsageReportKeepsUnreadableReset),
    ("Usage report ignores prose", UsageReportIgnoresProse),
    ("Usage report labels a model-specific week", UsageReportLabelsModelWeek),
    ("Main window starts with the quota icon", MainWindowStartsWithQuotaIcon)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed");
return failed == 0 ? 0 : 1;

static void UsageReportParsesRows()
{
    var snapshot = ClaudeUsageTextParser.Parse(UsageReport, DateTimeOffset.Parse("2026-09-12T02:42:00Z"));
    Equal(ProviderHealth.Available, snapshot.Health);
    Equal(2, snapshot.Windows.Count);
    Equal("five_hour", snapshot.Windows[0].Id);
    Equal("5h", snapshot.Windows[0].Label);
    Equal(31d, snapshot.Windows[0].UsedPercent);
    Equal(69d, snapshot.Windows[0].RemainingPercent);
    Equal("seven_day", snapshot.Windows[1].Id);
    Equal("7d", snapshot.Windows[1].Label);
}

static void UsageReportResolvesReset()
{
    var snapshot = ClaudeUsageTextParser.Parse(UsageReport, DateTimeOffset.Parse("2026-09-12T02:42:00Z"));
    // 1:20am America/New_York on Sep 12 is 05:20 UTC — the value the status line reported for five_hour.
    Equal(DateTimeOffset.Parse("2026-09-12T05:20:00Z"), snapshot.Windows[0].ResetsAt);
    Equal(DateTimeOffset.Parse("2026-09-17T06:00:00Z"), snapshot.Windows[1].ResetsAt);
}

static void UsageReportKeepsUnreadableReset()
{
    var snapshot = ClaudeUsageTextParser.Parse("Current session: 42% used · resets whenever it feels like it",
        DateTimeOffset.Parse("2026-09-12T02:42:00Z"));
    Equal(1, snapshot.Windows.Count);
    Equal(null, snapshot.Windows[0].ResetsAt);
    Equal(42d, snapshot.Windows[0].UsedPercent);
}

static void UsageReportIgnoresProse()
{
    const string prose = """
        Top skills: /run 20%, /code-review 2%
        Last 24h · 192 requests · 9 sessions
        """;
    var snapshot = ClaudeUsageTextParser.Parse(prose, DateTimeOffset.Parse("2026-09-12T02:42:00Z"));
    Equal(0, snapshot.Windows.Count);
    Equal(ProviderHealth.NotConfigured, snapshot.Health);
}

static void UsageReportLabelsModelWeek()
{
    var snapshot = ClaudeUsageTextParser.Parse("Current week (Opus): 12% used · resets Sep 17, 2am (America/New_York)",
        DateTimeOffset.Parse("2026-09-12T02:42:00Z"));
    Equal(1, snapshot.Windows.Count);
    Equal("7d Opus", snapshot.Windows[0].Label);
}

static void MainWindowStartsWithQuotaIcon()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            var assembly = Assembly.Load("AIQuotaTray");
            var formType = assembly.GetType("AIQuotaTray.MainForm", throwOnError: true)!;
            var factoryType = assembly.GetType("AIQuotaTray.TrayIconFactory", throwOnError: true)!;
            using var form = (Form)Activator.CreateInstance(formType, nonPublic: true)!;
            using var expected = (Icon)factoryType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, [null])!;

            Equal(IconFingerprint(expected), IconFingerprint(form.Icon ?? throw new InvalidOperationException("Main form has no icon.")));
        }
        catch (Exception exception)
        {
            failure = exception;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    if (failure is not null) throw failure;
}

static string IconFingerprint(Icon icon)
{
    using var bitmap = icon.ToBitmap();
    var pixels = new byte[bitmap.Width * bitmap.Height * sizeof(int)];
    var offset = 0;
    for (var y = 0; y < bitmap.Height; y++)
    for (var x = 0; x < bitmap.Width; x++)
    {
        BitConverter.TryWriteBytes(pixels.AsSpan(offset), bitmap.GetPixel(x, y).ToArgb());
        offset += sizeof(int);
    }
    return Convert.ToHexString(SHA256.HashData(pixels));
}

static void ClaudeParsesWindows()
{
    const string json = """
        {"rate_limits":{"five_hour":{"used_percentage":23.5,"resets_at":2000000000},"seven_day":{"used_percentage":41.2,"resets_at":2001000000}}}
        """;
    var snapshot = ClaudePayloadParser.Parse(json, DateTimeOffset.UnixEpoch);
    Equal(ProviderHealth.Available, snapshot.Health);
    Equal(2, snapshot.Windows.Count);
    Equal(76.5, snapshot.Windows[0].RemainingPercent);
    Equal("7d", snapshot.Windows[1].Label);
}

static void ClaudeHandlesMissingLimits()
{
    var snapshot = ClaudePayloadParser.Parse("{}", DateTimeOffset.UnixEpoch);
    Equal(ProviderHealth.NotConfigured, snapshot.Health);
    Equal(0, snapshot.Windows.Count);
}

static void CodexParsesBuckets()
{
    using var document = JsonDocument.Parse("""
        {"rateLimits":{"primary":{"usedPercent":99}},"rateLimitsByLimitId":{"codex":{"limitName":"Codex","primary":{"usedPercent":20,"windowDurationMins":300,"resetsAt":2000000000},"secondary":{"usedPercent":40,"windowDurationMins":10080,"resetsAt":2001000000}}}}
        """);
    var snapshot = CodexPayloadParser.Parse(document.RootElement, DateTimeOffset.UnixEpoch);
    Equal(2, snapshot.Windows.Count);
    Equal("5h", snapshot.Windows[0].Label);
    Equal("1w", snapshot.Windows[1].Label);
    Equal(80d, snapshot.Windows[0].RemainingPercent);
}

static void ExpiredWindowIsStale()
{
    var snapshot = new ProviderSnapshot(ProviderKind.Claude, ProviderHealth.Available, "ok",
        DateTimeOffset.UnixEpoch, [new UsageWindow("5h", "5h", 10, DateTimeOffset.UnixEpoch.AddHours(1))]);
    Equal(ProviderHealth.Stale, SnapshotRules.EffectiveHealth(snapshot, DateTimeOffset.UnixEpoch.AddHours(2)));
}

static void DurationLabelsAreCompact()
{
    Equal("5h", SnapshotRules.DurationLabel(300, "primary"));
    Equal("1w", SnapshotRules.DurationLabel(10080, "secondary"));
    Equal("primary", SnapshotRules.DurationLabel(null, "primary"));
}

static void LowestRemainingIgnoresStale()
{
    var future = DateTimeOffset.UtcNow.AddHours(1);
    var past = DateTimeOffset.UtcNow.AddHours(-1);
    var codex = new ProviderSnapshot(ProviderKind.Codex, ProviderHealth.Available, "ok", DateTimeOffset.UtcNow,
        [new UsageWindow("codex", "5h", 30, future)]);
    var claude = new ProviderSnapshot(ProviderKind.Claude, ProviderHealth.Available, "ok", DateTimeOffset.UtcNow,
        [new UsageWindow("claude", "5h", 99, past)]);
    Equal(70d, new ApplicationSnapshot(codex, claude).LowestRemaining(DateTimeOffset.UtcNow));
}

static void NewestObservationWins()
{
    var older = ProviderSnapshot.Empty(ProviderKind.Claude, ProviderHealth.Available, "older") with
    {
        ObservedAt = DateTimeOffset.UnixEpoch.AddMinutes(1)
    };
    var newer = older with { ObservedAt = DateTimeOffset.UnixEpoch.AddMinutes(2), StatusMessage = "newer" };
    Equal(true, SnapshotRules.ShouldReplace(older, newer));
    Equal(false, SnapshotRules.ShouldReplace(newer, older));
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, received {actual}.");
    }
}
