using AIQuotaTray.Core;

namespace AIQuotaTray;

internal sealed class ProviderCard : Panel
{
    private const int CornerRadius = 12;

    private readonly Color _accent;
    private readonly AccentDot _dot = new();
    private readonly Label _title = new();
    private readonly StatusPill _statusPill = new();
    private readonly Label _statusDetail = new();
    private readonly Panel _header = new();
    private readonly FlowLayoutPanel _windows = new()
    {
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Margin = new Padding(0, 14, 0, 0)
    };

    public ProviderCard(string title, Color accent)
    {
        _accent = accent;
        Padding = new Padding(18, 16, 18, 18);
        Margin = new Padding(0, 0, 0, 12);
        AutoSize = false;
        Dock = DockStyle.Top;

        _dot.FillColor = accent;
        _dot.Location = new Point(0, 5);

        _title.Text = title;
        _title.Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 13, FontStyle.Bold);
        _title.AutoSize = true;
        _title.Location = new Point(_dot.Right + 8, 0);

        _statusPill.StatusText = "…";
        _statusPill.Location = new Point(0, 0);

        _header.Height = 24;
        _header.Margin = new Padding(0);
        _header.Controls.Add(_dot);
        _header.Controls.Add(_title);
        _header.Controls.Add(_statusPill);

        _statusDetail.AutoSize = true;
        _statusDetail.Margin = new Padding(0, 6, 0, 0);

        var top = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0)
        };
        top.Controls.Add(_header);
        top.Controls.Add(_statusDetail);
        top.Controls.Add(_windows);

        Controls.Add(top);
        UpdateRegion();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRegion();
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        using var path = RoundedRect.Path(new Rectangle(0, 0, Width, Height), CornerRadius);
        Region?.Dispose();
        Region = new Region(path);
    }

    public void UpdateSnapshot(ProviderSnapshot snapshot, DateTimeOffset now)
    {
        var effectiveHealth = SnapshotRules.EffectiveHealth(snapshot, now);
        var kind = HealthKind(effectiveHealth);
        var age = UsageFormatting.RelativeAge(snapshot.ObservedAt, now);

        _statusPill.StatusText = kind == ProviderHealthKind.Available ? "CONNECTED" : HealthText(effectiveHealth).ToUpperInvariant();
        _statusPill.Accent = ThemeColors.Status(kind);
        _statusDetail.Text = kind == ProviderHealthKind.Available
            ? $"Updated {age}"
            : snapshot.StatusMessage;
        _statusDetail.ForeColor = kind == ProviderHealthKind.Available ? ThemeColors.MutedText : ThemeColors.Status(kind);
        RepositionHeader();

        _windows.SuspendLayout();
        _windows.Controls.Clear();
        foreach (var window in snapshot.Windows)
        {
            _windows.Controls.Add(CreateWindowRow(window, now, kind == ProviderHealthKind.Available, _windows.Width));
        }

        if (snapshot.Windows.Count == 0)
        {
            _windows.Controls.Add(new Label
            {
                Text = "No usage windows available",
                AutoSize = true,
                ForeColor = ThemeColors.MutedText
            });
        }
        _windows.ResumeLayout();

        ApplyTheme();
        AutoFitHeight();
    }

    public void SetContentWidth(int width)
    {
        Width = width;
        var innerWidth = Math.Max(240, width - Padding.Horizontal);
        _header.Width = innerWidth;
        RepositionHeader();
        foreach (Control row in _windows.Controls)
        {
            foreach (Control control in row.Controls)
            {
                if (control is GaugeBar gauge) gauge.Width = innerWidth;
            }
        }
        AutoFitHeight();
    }

    private void RepositionHeader()
    {
        _statusPill.Location = new Point(Math.Max(_title.Right + 8, _header.Width - _statusPill.Width), 0);
    }

    private void AutoFitHeight()
    {
        // The content flows top-down and reports its own preferred height; reading it back here means
        // this card never needs a hand-maintained "rows * row height" formula to size itself.
        var contentTop = Controls[0];
        Height = contentTop.Bottom + Padding.Bottom;
        UpdateRegion();
    }

    private static Control CreateWindowRow(UsageWindow window, DateTimeOffset now, bool trustworthy, int width)
    {
        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 14)
        };
        var remaining = new Label
        {
            Text = $"{window.Label}   ·   {UsageFormatting.Percent(window.RemainingPercent)} remaining",
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 10.5f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        var used = new Label
        {
            Text = $"{UsageFormatting.Percent(window.UsedPercent)} used · {UsageFormatting.ResetText(window.ResetsAt, now)}",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            ForeColor = ThemeColors.MutedText
        };
        var gauge = new GaugeBar
        {
            Width = width,
            Value = trustworthy ? window.RemainingPercent : 0,
            FillColor = trustworthy ? ThemeColors.Gauge(window.RemainingPercent) : ThemeColors.Gray,
            Margin = new Padding(0),
            AccessibleName = $"{window.Label} remaining usage",
            AccessibleDescription = $"{UsageFormatting.Percent(window.RemainingPercent)} remaining"
        };
        row.Controls.Add(remaining);
        row.Controls.Add(used);
        row.Controls.Add(gauge);
        return row;
    }

    private void ApplyTheme()
    {
        BackColor = ThemeColors.Surface;
        _title.ForeColor = ThemeColors.Foreground;
        _title.BackColor = ThemeColors.Surface;
        _dot.BackColor = ThemeColors.Surface;
        _header.BackColor = ThemeColors.Surface;
        _statusDetail.BackColor = ThemeColors.Surface;
        foreach (Control row in _windows.Controls)
        {
            row.BackColor = ThemeColors.Surface;
            foreach (Control child in row.Controls)
            {
                if (child is not GaugeBar && child.ForeColor != ThemeColors.MutedText) child.ForeColor = ThemeColors.Foreground;
                child.BackColor = ThemeColors.Surface;
            }
        }
    }

    private static ProviderHealthKind HealthKind(ProviderHealth health) => health switch
    {
        ProviderHealth.Available => ProviderHealthKind.Available,
        ProviderHealth.Connecting => ProviderHealthKind.Connecting,
        ProviderHealth.Stale => ProviderHealthKind.Attention,
        ProviderHealth.SignInRequired => ProviderHealthKind.Attention,
        ProviderHealth.NotConfigured => ProviderHealthKind.Connecting,
        ProviderHealth.CliNotFound => ProviderHealthKind.Error,
        _ => ProviderHealthKind.Error
    };

    private static string HealthText(ProviderHealth health) => health switch
    {
        ProviderHealth.Stale => "Stale",
        ProviderHealth.SignInRequired => "Sign-in required",
        ProviderHealth.NotConfigured => "Waiting",
        ProviderHealth.CliNotFound => "CLI not found",
        ProviderHealth.Connecting => "Connecting",
        _ => "Unavailable"
    };
}
