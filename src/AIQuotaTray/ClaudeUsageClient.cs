using System.Diagnostics;
using System.Text.Json;
using AIQuotaTray.Core;

namespace AIQuotaTray;

/// Polls Claude Code for subscription usage by running `claude -p "/usage" --output-format json`.
/// The command dispatches locally — it reports zero turns and zero cost — so polling it does not spend
/// any of the allowance it is reporting on.
internal sealed class ClaudeUsageClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    /// <param name="waitIfBusy">
    /// Mirrors the Codex client: <c>false</c> drops a poll that collides with one already running,
    /// <c>true</c> queues behind it so an explicit request always produces a fresh read.
    /// </param>
    public async Task<ProviderSnapshot?> RefreshAsync(bool waitIfBusy = false, CancellationToken cancellationToken = default)
    {
        if (waitIfBusy) await _refreshGate.WaitAsync(cancellationToken);
        else if (!await _refreshGate.WaitAsync(0, cancellationToken)) return null;

        try
        {
            return await ReadUsageAsync(cancellationToken);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return ProviderSnapshot.Empty(ProviderKind.Claude, ProviderHealth.CliNotFound,
                "Claude Code CLI was not found");
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException)
        {
            return ProviderSnapshot.Empty(ProviderKind.Claude, ProviderHealth.Error,
                "Could not read Claude usage");
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private static async Task<ProviderSnapshot> ReadUsageAsync(CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ResolveClaudeExecutable(),
                // ArgumentList quotes each value itself, so "/usage" reaches the CLI intact without a
                // shell in the middle to rewrite it into a path.
                ArgumentList = { "-p", "/usage", "--output-format", "json" },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Timeout);
        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return ProviderSnapshot.Empty(ProviderKind.Claude, ProviderHealth.Error,
                "Claude Code did not respond in time");
        }

        await stderr;
        var payload = await stdout;
        if (process.ExitCode != 0 || payload.Length == 0)
        {
            return ProviderSnapshot.Empty(ProviderKind.Claude, ProviderHealth.Error,
                "Claude Code could not report usage");
        }

        return ParseEnvelope(payload, DateTimeOffset.UtcNow);
    }

    /// The report arrives as the `result` string of the CLI's JSON envelope.
    private static ProviderSnapshot ParseEnvelope(string payload, DateTimeOffset observedAt)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        if (root.TryGetProperty("is_error", out var isError) && isError.ValueKind == JsonValueKind.True)
        {
            return ProviderSnapshot.Empty(ProviderKind.Claude, ProviderHealth.Error,
                "Claude Code reported an error");
        }

        if (!root.TryGetProperty("result", out var result) || result.GetString() is not { Length: > 0 } report)
        {
            return ProviderSnapshot.Empty(ProviderKind.Claude, ProviderHealth.Error,
                "Claude Code returned no usage report");
        }

        return ClaudeUsageTextParser.Parse(report, observedAt);
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(true); }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    private static string ResolveClaudeExecutable()
    {
        var installed = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "claude.exe");
        return File.Exists(installed) ? installed : "claude.exe";
    }
}
