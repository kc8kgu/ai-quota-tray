using System.Text.Json;
using AIQuotaTray.Core;

var tests = new (string Name, Action Run)[]
{
    ("Claude parses subscription windows", ClaudeParsesWindows),
    ("Claude handles missing limits", ClaudeHandlesMissingLimits),
    ("Codex prefers multi-bucket limits", CodexParsesBuckets),
    ("Expired window makes snapshot stale", ExpiredWindowIsStale),
    ("Duration labels are compact", DurationLabelsAreCompact),
    ("Lowest remaining ignores stale providers", LowestRemainingIgnoresStale),
    ("Newest Claude observation wins", NewestObservationWins)
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
