using System.Diagnostics;
using AIQuotaTray.Core;

namespace AIQuotaTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SynchronizationContext _uiContext;
    private readonly CodexAppServerClient _codexClient = new();
    private readonly ClaudeUsageClient _claudeClient = new();
    private readonly MainForm _window;
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private AppSettings _settings;
    private ApplicationSnapshot _snapshot;
    private DateTimeOffset _lastCodexRefresh = DateTimeOffset.MinValue;
    private DateTimeOffset _lastClaudeRefresh = DateTimeOffset.MinValue;
    private Icon? _dynamicIcon;
    private bool _exiting;

    public TrayApplicationContext(bool startedWithWindows)
    {
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _settings = AppSettings.Load();
        _snapshot = AtomicJsonStore.Read<ApplicationSnapshot>(AppPaths.SnapshotFile) ?? new ApplicationSnapshot(
            ProviderSnapshot.Empty(ProviderKind.Codex, ProviderHealth.Connecting, "Connecting to Codex"),
            AtomicJsonStore.Read<ProviderSnapshot>(AppPaths.ClaudeSnapshotFile) ??
                ProviderSnapshot.Empty(ProviderKind.Claude, ProviderHealth.NotConfigured, "Waiting for Claude Code"));

        var startWithWindows = false;
        try
        {
            StartupService.SetEnabled(_settings.StartWithWindows);
            startWithWindows = StartupService.IsEnabled();
        }
        catch (UnauthorizedAccessException) { }
        _window = new MainForm(startWithWindows);
        _window.RefreshRequested += async (_, _) => await RefreshAllAsync(userInitiated: true);
        _window.CodexLoginRequested += async (_, _) => await SignInToCodexAsync();
        _window.OpenClaudeRequested += (_, _) => OpenClaude();
        _window.StartWithWindowsChanged += (_, enabled) => SetStartWithWindows(enabled);
        _window.FormClosed += (_, _) => ExitThread();

        var menu = new ContextMenuStrip();
        var openItem = (ToolStripMenuItem)menu.Items.Add("Open AIQuotaTray", null, (_, _) => ShowWindow());
        openItem.Font = new Font(menu.Font, FontStyle.Bold); // matches the double-click default action
        menu.Items.Add("Refresh now", null, async (_, _) => await RefreshAllAsync(userInitiated: true));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit AIQuotaTray", null, (_, _) => Exit());

        _trayIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Visible = true,
            Text = "AIQuotaTray"
        };
        _trayIcon.DoubleClick += (_, _) => ShowWindow();

        _codexClient.SnapshotChanged += (_, snapshot) =>
            _uiContext.Post(_ => UpdateCodex(snapshot), null);

        _timer = new System.Windows.Forms.Timer { Interval = 10_000 };
        _timer.Tick += async (_, _) => await OnTimerTickAsync();
        _timer.Start();

        UpdatePresentation();
        if (!_settings.FirstRunComplete || !startedWithWindows) ShowWindow();
        _ = RefreshAllAsync();

        if (!_settings.FirstRunComplete)
        {
            _settings = _settings with { FirstRunComplete = true };
            _settings.Save();
        }
    }

    private async Task OnTimerTickAsync()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastCodexRefresh >= TimeSpan.FromMinutes(1)) await RefreshCodexAsync();
        if (now - _lastClaudeRefresh >= TimeSpan.FromMinutes(1)) await RefreshClaudeAsync();
        UpdatePresentation();
    }

    /// Runs on the UI thread from an async void event handler, so it has to swallow what it cannot show:
    /// an escaping exception here would tear down the process instead of surfacing as card status text.
    private async Task RefreshAllAsync(bool userInitiated = false)
    {
        _window.SetRefreshing(true);
        try
        {
            await Task.WhenAll(
                RefreshCodexAsync(waitIfBusy: userInitiated),
                RefreshClaudeAsync(waitIfBusy: userInitiated));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // A provider that cannot be read reports itself through its own snapshot health.
        }
        finally
        {
            _window.SetRefreshing(false);
            UpdatePresentation();
        }
    }

    private async Task RefreshCodexAsync(bool waitIfBusy = false)
    {
        _lastCodexRefresh = DateTimeOffset.UtcNow;
        await _codexClient.RefreshAsync(waitIfBusy);
    }

    private async Task RefreshClaudeAsync(bool waitIfBusy = false)
    {
        _lastClaudeRefresh = DateTimeOffset.UtcNow;
        var claude = await _claudeClient.RefreshAsync(waitIfBusy);
        if (claude is null) return;
        _snapshot = _snapshot with { Claude = claude };
        SaveSnapshot();
    }

    private void UpdateCodex(ProviderSnapshot codex)
    {
        _snapshot = _snapshot with { Codex = codex };
        SaveSnapshot();
        UpdatePresentation();
    }

    private void SaveSnapshot()
    {
        try { AtomicJsonStore.Write(AppPaths.SnapshotFile, _snapshot); } catch (IOException) { }
    }

    private void UpdatePresentation()
    {
        var now = DateTimeOffset.UtcNow;
        _window.UpdateSnapshot(_snapshot, now);
        var remaining = _snapshot.LowestRemaining(now);
        var nextIcon = TrayIconFactory.Create(remaining);
        var oldIcon = _dynamicIcon;
        _dynamicIcon = nextIcon;
        _trayIcon.Icon = nextIcon;
        oldIcon?.Dispose();
        _trayIcon.Text = BuildTooltip(now);
    }

    private string BuildTooltip(DateTimeOffset now)
    {
        static string ProviderText(ProviderSnapshot provider, DateTimeOffset timestamp)
        {
            if (SnapshotRules.EffectiveHealth(provider, timestamp) != ProviderHealth.Available) return "--";
            return string.Join("/", provider.Windows.Select(window => UsageFormatting.Percent(window.RemainingPercent)));
        }

        var tooltip = $"AIQuotaTray | Codex {ProviderText(_snapshot.Codex, now)} | Claude {ProviderText(_snapshot.Claude, now)}";
        return tooltip.Length <= 63 ? tooltip : tooltip[..63];
    }

    private async Task SignInToCodexAsync()
    {
        try { await _codexClient.BeginLoginAsync(); }
        catch (Exception exception)
        {
            MessageBox.Show(_window, $"Could not start Codex sign-in.\n\n{exception.Message}", "AIQuotaTray",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void OpenClaude()
    {
        try { Process.Start(new ProcessStartInfo("claude") { UseShellExecute = true }); }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show($"Could not open Claude Code.\n\n{exception.Message}", "AIQuotaTray",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetStartWithWindows(bool enabled)
    {
        try
        {
            StartupService.SetEnabled(enabled);
            _settings = _settings with { StartWithWindows = enabled };
            _settings.Save();
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(_window, "Windows did not allow the startup setting to be changed.", "AIQuotaTray",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowWindow()
    {
        _window.ShowInTaskbar = true;
        _window.Show();
        _window.WindowState = FormWindowState.Normal;
        _window.Activate();
    }

    private void Exit()
    {
        if (_exiting) return;
        _exiting = true;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _dynamicIcon?.Dispose();
            _codexClient.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _window.Dispose();
        }
        base.Dispose(disposing);
    }
}
