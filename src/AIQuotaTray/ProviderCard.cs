using AIQuotaTray.Core;
using System.Drawing.Drawing2D;

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
        Margin = new Padding(0, 6, 0, 0)
    };

    public ProviderCard(string title, Color accent)
    {
        _accent = accent;
        Padding = new Padding(18, 14, 18, 14);
        Margin = new Padding(0, 0, 0, 12);
        AutoSize = false;
        Dock = DockStyle.Top;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

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
        _statusDetail.Margin = new Padding(_title.Left, 2, 0, 0);

        var top = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0)
        };
        top.BackColor = ThemeColors.Surface;
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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width <= 1 || Height <= 1) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var rail = new SolidBrush(_accent);
        e.Graphics.FillRectangle(rail, 0, 0, 5, Height);
        using var path = RoundedRect.Path(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
        using var border = new Pen(ThemeColors.Border);
        e.Graphics.DrawPath(border, path);
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
        while (_windows.Controls.Count > 0)
        {
            var control = _windows.Controls[0];
            _windows.Controls.RemoveAt(0);
            control.Dispose();
        }
        for (var index = 0; index < snapshot.Windows.Count; index++)
        {
            _windows.Controls.Add(new UsageWindowRow(snapshot.Windows[index], now, kind == ProviderHealthKind.Available)
            {
                Width = _windows.Width,
                ShowDivider = index < snapshot.Windows.Count - 1
            });
        }

        if (snapshot.Windows.Count == 0)
        {
            _windows.Controls.Add(new Label
            {
                Text = "No usage windows available",
                AutoSize = false,
                Height = 42,
                Width = _windows.Width,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ThemeColors.MutedText,
                Margin = new Padding(0)
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
        _statusDetail.MaximumSize = new Size(Math.Max(80, innerWidth - _statusDetail.Margin.Left), 0);
        RepositionHeader();
        _windows.Width = innerWidth;
        foreach (Control row in _windows.Controls) row.Width = innerWidth;
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

    private sealed class UsageWindowRow : Panel
    {
        private readonly Label _period;
        private readonly Label _remaining;
        private readonly Label _used;
        private readonly Panel _verticalDivider;
        private readonly Panel _horizontalDivider = new()
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            Visible = false,
            Tag = "divider"
        };

        public bool ShowDivider
        {
            get => _horizontalDivider.Visible;
            init => _horizontalDivider.Visible = value;
        }

        public UsageWindowRow(UsageWindow window, DateTimeOffset now, bool trustworthy)
        {
            Height = 78;
            Margin = new Padding(0);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _period = new Label
            {
                Text = window.Label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 11.5f, FontStyle.Bold),
                Margin = new Padding(0)
            };
            _verticalDivider = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 12, 0, 12),
                Tag = "divider"
            };

            var metric = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(16, 3, 0, 7)
            };
            metric.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            metric.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            metric.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));

            _remaining = new Label
            {
                Text = $"{UsageFormatting.Percent(window.RemainingPercent)} remaining",
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 11, FontStyle.Bold),
                Margin = new Padding(0)
            };
            _used = new Label
            {
                Text = $"{UsageFormatting.Percent(window.UsedPercent)} used · {UsageFormatting.ResetText(window.ResetsAt, now)}",
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0)
            };
            var gauge = new GaugeBar
            {
                Dock = DockStyle.Top,
                Height = 6,
                Value = trustworthy ? window.RemainingPercent : 0,
                FillColor = trustworthy ? ThemeColors.Gauge(window.RemainingPercent) : ThemeColors.Gray,
                Margin = new Padding(0),
                AccessibleName = $"{window.Label} remaining usage",
                AccessibleDescription = $"{UsageFormatting.Percent(window.RemainingPercent)} remaining"
            };

            metric.Controls.Add(_remaining, 0, 0);
            metric.Controls.Add(_used, 0, 1);
            metric.Controls.Add(gauge, 0, 2);
            layout.Controls.Add(_period, 0, 0);
            layout.Controls.Add(_verticalDivider, 1, 0);
            layout.Controls.Add(metric, 2, 0);
            Controls.Add(layout);
            Controls.Add(_horizontalDivider);
            _horizontalDivider.BringToFront();
            ApplyTheme();
        }

        public void ApplyTheme()
        {
            BackColor = ThemeColors.Surface;
            _period.ForeColor = ThemeColors.Foreground;
            _remaining.ForeColor = ThemeColors.Foreground;
            _used.ForeColor = ThemeColors.MutedText;
            _verticalDivider.BackColor = ThemeColors.Border;
            _horizontalDivider.BackColor = ThemeColors.Border;
            ApplyBackground(Controls);
            Invalidate();
        }

        private static void ApplyBackground(Control.ControlCollection controls)
        {
            foreach (Control control in controls)
            {
                if (control is not GaugeBar && control.Tag as string != "divider") control.BackColor = ThemeColors.Surface;
                ApplyBackground(control.Controls);
            }
        }
    }

    private void ApplyTheme()
    {
        BackColor = ThemeColors.Surface;
        _title.ForeColor = ThemeColors.Foreground;
        _title.BackColor = ThemeColors.Surface;
        _dot.BackColor = ThemeColors.Surface;
        _header.BackColor = ThemeColors.Surface;
        _statusDetail.BackColor = ThemeColors.Surface;
        _windows.BackColor = ThemeColors.Surface;
        foreach (Control row in _windows.Controls)
        {
            row.BackColor = ThemeColors.Surface;
            if (row is UsageWindowRow usageRow)
            {
                usageRow.ApplyTheme();
                continue;
            }
            foreach (Control child in row.Controls)
            {
                if (child is not GaugeBar && child.ForeColor != ThemeColors.MutedText) child.ForeColor = ThemeColors.Foreground;
                child.BackColor = ThemeColors.Surface;
            }
        }
        Invalidate();
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
