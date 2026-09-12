using AIQuotaTray.Core;

namespace AIQuotaTray;

internal sealed class ProviderCard : Panel
{
    private readonly Label _title = new();
    private readonly Label _status = new();
    private readonly Panel _windows = new();

    public ProviderCard(string title)
    {
        Padding = new Padding(16);
        Margin = new Padding(0, 0, 0, 12);
        AutoSize = false;
        Dock = DockStyle.Top;

        _title.Text = title;
        _title.Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 14, FontStyle.Bold);
        _title.AutoSize = true;
        _title.Location = new Point(16, 14);

        _status.AutoSize = true;
        _status.Location = new Point(17, 44);

        _windows.Location = new Point(16, 72);
        _windows.Width = 420;

        Controls.Add(_title);
        Controls.Add(_status);
        Controls.Add(_windows);
        Height = 112;
    }

    public void UpdateSnapshot(ProviderSnapshot snapshot, DateTimeOffset now)
    {
        var effectiveHealth = SnapshotRules.EffectiveHealth(snapshot, now);
        var age = UsageFormatting.RelativeAge(snapshot.ObservedAt, now);
        _status.Text = effectiveHealth == ProviderHealth.Available
            ? $"Connected · updated {age}"
            : $"{HealthText(effectiveHealth)} · {snapshot.StatusMessage}";
        _status.ForeColor = effectiveHealth == ProviderHealth.Available ? ThemeColors.MutedText : ThemeColors.Gauge(null);

        _windows.SuspendLayout();
        _windows.Controls.Clear();
        var top = 4;
        foreach (var window in snapshot.Windows)
        {
            var row = CreateWindowRow(window, now, effectiveHealth == ProviderHealth.Available, _windows.Width);
            row.Location = new Point(0, top);
            _windows.Controls.Add(row);
            top = row.Bottom + 8;
        }

        if (snapshot.Windows.Count == 0)
        {
            var empty = new Label
            {
                Text = "No usage windows available",
                AutoSize = true,
                ForeColor = ThemeColors.MutedText,
                Location = new Point(0, top)
            };
            _windows.Controls.Add(empty);
            top = empty.Bottom + 8;
        }
        _windows.Height = top;
        Height = snapshot.Windows.Count == 0 ? 116 : 84 + (snapshot.Windows.Count * 84);
        _windows.ResumeLayout();
        ApplyTheme();
    }

    public void SetContentWidth(int width)
    {
        Width = width;
        _windows.Width = Math.Max(280, width - 32);
        foreach (Control row in _windows.Controls)
        {
            row.Width = _windows.Width;
            foreach (Control control in row.Controls)
            {
                if (control is GaugeBar) control.Width = _windows.Width;
            }
        }
    }

    private static Control CreateWindowRow(UsageWindow window, DateTimeOffset now, bool trustworthy, int width)
    {
        var panel = new Panel { Width = width, Height = 76, Margin = new Padding(0, 4, 0, 4) };
        var remaining = new Label
        {
            Text = $"{window.Label}   {UsageFormatting.Percent(window.RemainingPercent)} remaining",
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 11, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 0)
        };
        var used = new Label
        {
            Text = $"{UsageFormatting.Percent(window.UsedPercent)} used · {UsageFormatting.ResetText(window.ResetsAt, now)}",
            AutoSize = true,
            Location = new Point(0, 27),
            ForeColor = ThemeColors.MutedText
        };
        var gauge = new GaugeBar
        {
            Width = width,
            Value = trustworthy ? window.RemainingPercent : 0,
            FillColor = trustworthy ? ThemeColors.Gauge(window.RemainingPercent) : ThemeColors.Gray,
            Location = new Point(0, 54),
            AccessibleName = $"{window.Label} remaining usage",
            AccessibleDescription = $"{UsageFormatting.Percent(window.RemainingPercent)} remaining"
        };
        panel.Controls.Add(remaining);
        panel.Controls.Add(used);
        panel.Controls.Add(gauge);
        return panel;
    }

    private void ApplyTheme()
    {
        BackColor = ThemeColors.Surface;
        _title.ForeColor = ThemeColors.Foreground;
        foreach (Control control in _windows.Controls)
        {
            control.BackColor = ThemeColors.Surface;
            foreach (Control child in control.Controls)
            {
                if (child is not GaugeBar && child.ForeColor != ThemeColors.MutedText) child.ForeColor = ThemeColors.Foreground;
                child.BackColor = ThemeColors.Surface;
            }
        }
    }

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
