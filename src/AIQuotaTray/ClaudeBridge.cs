using AIQuotaTray.Core;

namespace AIQuotaTray;

internal static class ClaudeBridge
{
    public static int Run()
    {
        try
        {
            var payload = Console.In.ReadToEnd();
            var snapshot = ClaudePayloadParser.Parse(payload, DateTimeOffset.UtcNow);
            ClaudeSnapshotStore.WriteLatest(snapshot);

            var parts = snapshot.Windows.Select(window =>
                $"{window.Label} {UsageFormatting.Percent(window.RemainingPercent)} left");
            Console.Out.WriteLine(snapshot.Windows.Count == 0
                ? "Claude · usage available after first response"
                : $"Claude · {string.Join(" · ", parts)}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Console.Error.WriteLine($"AIQuotaTray bridge: {exception.Message}");
            return 1;
        }
    }
}
