using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIQuotaTray.Core;

namespace AIQuotaTray;

internal sealed class CodexAppServerClient : IAsyncDisposable
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private Process? _process;
    private StreamWriter? _input;
    private Task? _readerTask;
    private long _nextId;

    public event EventHandler<ProviderSnapshot>? SnapshotChanged;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!await _refreshGate.WaitAsync(0, cancellationToken)) return;
        try
        {
            await EnsureStartedAsync(cancellationToken);
            var account = await SendRequestAsync("account/read", new { refreshToken = false }, cancellationToken);
            if (!IsSubscriptionAccount(account))
            {
                Publish(ProviderSnapshot.Empty(ProviderKind.Codex, ProviderHealth.SignInRequired,
                    "Sign in to Codex with ChatGPT"));
                return;
            }

            var limits = await SendRequestAsync("account/rateLimits/read", null, cancellationToken);
            Publish(CodexPayloadParser.Parse(limits, DateTimeOffset.UtcNow));
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Publish(ProviderSnapshot.Empty(ProviderKind.Codex, ProviderHealth.CliNotFound,
                "Codex CLI was not found"));
            await ResetProcessAsync();
        }
        catch (Exception exception) when (exception is IOException or JsonException or RpcException or InvalidOperationException)
        {
            Publish(ProviderSnapshot.Empty(ProviderKind.Codex, ProviderHealth.Error,
                FriendlyError(exception.Message)));
            await ResetProcessAsync();
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public async Task BeginLoginAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var result = await SendRequestAsync("account/login/start", new
        {
            type = "chatgpt",
            useHostedLoginSuccessPage = true,
            appBrand = "codex"
        }, cancellationToken);

        if (result.TryGetProperty("authUrl", out var url) && url.GetString() is { Length: > 0 } authUrl)
        {
            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });
        }
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_process is { HasExited: false }) return;
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (_process is { HasExited: false }) return;

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ResolveCodexExecutable(),
                    Arguments = "app-server --stdio",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };
            process.ErrorDataReceived += (_, _) => { };
            process.Start();
            process.BeginErrorReadLine();
            _process = process;
            _input = process.StandardInput;
            _readerTask = Task.Run(() => ReadLoopAsync(process, _lifetime.Token), _lifetime.Token);

            await SendRequestAsync("initialize", new
            {
                clientInfo = new { name = "ai_quota_tray", title = "AIQuotaTray", version = "0.1.0" },
                capabilities = new { experimentalApi = false }
            }, cancellationToken);
            await SendNotificationAsync("initialized", null, cancellationToken);
        }
        catch
        {
            ResetProcessWithoutLock();
            throw;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task<JsonElement> SendRequestAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;
        try
        {
            await WriteMessageAsync(new { method, id, @params = parameters }, cancellationToken);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private Task SendNotificationAsync(string method, object? parameters, CancellationToken cancellationToken) =>
        WriteMessageAsync(new { method, @params = parameters }, cancellationToken);

    private async Task WriteMessageAsync(object message, CancellationToken cancellationToken)
    {
        var input = _input ?? throw new InvalidOperationException("Codex App Server is not running.");
        var json = JsonSerializer.Serialize(message, JsonOptions);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            await input.WriteLineAsync(json.AsMemory(), cancellationToken);
            await input.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task ReadLoopAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.TryGetProperty("id", out var idElement) && idElement.TryGetInt64(out var id) &&
                    _pending.TryGetValue(id, out var completion))
                {
                    if (root.TryGetProperty("error", out var error))
                    {
                        completion.TrySetException(new RpcException(ReadRpcError(error)));
                    }
                    else if (root.TryGetProperty("result", out var result))
                    {
                        completion.TrySetResult(result.Clone());
                    }
                    continue;
                }

                if (root.TryGetProperty("method", out var method) &&
                    method.GetString() == "account/rateLimits/updated")
                {
                    _ = Task.Run(() => RefreshAsync(cancellationToken), cancellationToken);
                }
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                FailPending("Codex App Server stopped unexpectedly.");
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or OperationCanceledException or InvalidOperationException)
        {
            if (!cancellationToken.IsCancellationRequested) FailPending(exception.Message);
        }
    }

    private async Task ResetProcessAsync()
    {
        await _lifecycleGate.WaitAsync();
        try
        {
            ResetProcessWithoutLock();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private void ResetProcessWithoutLock()
    {
        var process = _process;
        _process = null;
        _input = null;
        if (process is null) return;
        if (!process.HasExited) process.Kill(true);
        process.Dispose();
    }

    private void Publish(ProviderSnapshot snapshot) => SnapshotChanged?.Invoke(this, snapshot);

    private static bool IsSubscriptionAccount(JsonElement result)
    {
        if (!result.TryGetProperty("account", out var account) || account.ValueKind != JsonValueKind.Object) return false;
        return account.TryGetProperty("type", out var type) && type.GetString() == "chatgpt";
    }

    private static string ReadRpcError(JsonElement error) =>
        error.TryGetProperty("message", out var message) ? message.GetString() ?? "Codex request failed" : "Codex request failed";

    private static string FriendlyError(string message) =>
        message.Contains("auth", StringComparison.OrdinalIgnoreCase)
            ? "Codex sign-in is required"
            : "Codex connection failed";

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        FailPending("AIQuotaTray is shutting down.");
        await ResetProcessAsync();
        if (_readerTask is not null)
        {
            try { await _readerTask; } catch (Exception exception) when (exception is OperationCanceledException or IOException or InvalidOperationException) { }
        }
        _lifetime.Dispose();
        _lifecycleGate.Dispose();
        _writeGate.Dispose();
        _refreshGate.Dispose();
    }

    private void FailPending(string message)
    {
        foreach (var completion in _pending.Values)
        {
            completion.TrySetException(new RpcException(message));
        }
    }

    private static string ResolveCodexExecutable()
    {
        var installRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        try
        {
            var installed = Directory.Exists(installRoot)
                ? Directory.EnumerateFiles(installRoot, "codex.exe", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault()
                : null;
            return installed ?? "codex.exe";
        }
        catch (IOException)
        {
            return "codex.exe";
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class RpcException(string message) : Exception(message);
}
