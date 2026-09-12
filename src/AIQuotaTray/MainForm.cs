using AIQuotaTray.Core;

namespace AIQuotaTray;

internal sealed class MainForm : Form
{
    private readonly ProviderCard _codexCard = new("Codex");
    private readonly ProviderCard _claudeCard = new("Claude");
    private readonly Panel _cards = new();
    private readonly CheckBox _startWithWindows = new();
    private readonly Label _claudeIntegration = new();
    public event EventHandler? RefreshRequested;
    public event EventHandler? CodexLoginRequested;
    public event EventHandler? OpenClaudeRequested;
    public event EventHandler<bool>? StartWithWindowsChanged;

    public MainForm(bool startWithWindows)
    {
        Text = "AIQuotaTray";
        ClientSize = new Size(560, 780);
        MinimumSize = new Size(520, 680);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 6
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var heading = new Label
        {
            Text = "AI subscription usage",
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 18, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        };
        var subtitle = new Label
        {
            Text = "Remaining allowance across your active accounts",
            AutoSize = true,
            Margin = new Padding(2, 0, 0, 16)
        };

        _cards.AutoScroll = true;
        _cards.Dock = DockStyle.Fill;
        _cards.Margin = new Padding(0);
        _codexCard.Dock = DockStyle.None;
        _claudeCard.Dock = DockStyle.None;
        _cards.Controls.Add(_codexCard);
        _cards.Controls.Add(_claudeCard);
        _cards.ClientSizeChanged += (_, _) => ResizeCards();

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 12, 0, 12)
        };
        var refresh = new Button { Text = "Refresh now", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        var signIn = new Button { Text = "Sign in to Codex", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        signIn.Click += (_, _) => CodexLoginRequested?.Invoke(this, EventArgs.Empty);
        var openClaude = new Button { Text = "Open Claude Code", AutoSize = true, Margin = new Padding(0) };
        openClaude.Click += (_, _) => OpenClaudeRequested?.Invoke(this, EventArgs.Empty);
        actions.Controls.Add(refresh);
        actions.Controls.Add(signIn);
        actions.Controls.Add(openClaude);

        _startWithWindows.Text = "Start AIQuotaTray with Windows";
        _startWithWindows.AutoSize = true;
        _startWithWindows.Checked = startWithWindows;
        _startWithWindows.Margin = new Padding(0, 0, 0, 8);
        _startWithWindows.CheckedChanged += (_, _) => StartWithWindowsChanged?.Invoke(this, _startWithWindows.Checked);

        _claudeIntegration.AutoSize = true;
        _claudeIntegration.Margin = new Padding(0);

        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(subtitle, 0, 1);
        root.Controls.Add(_cards, 0, 2);
        root.Controls.Add(actions, 0, 3);
        root.Controls.Add(_startWithWindows, 0, 4);
        root.Controls.Add(_claudeIntegration, 0, 5);
        Controls.Add(root);

        FormClosing += OnFormClosing;
        ApplyTheme();
    }

    public void UpdateSnapshot(ApplicationSnapshot snapshot, DateTimeOffset now)
    {
        _codexCard.UpdateSnapshot(snapshot.Codex, now);
        _claudeCard.UpdateSnapshot(snapshot.Claude, now);
        _claudeIntegration.Text = ClaudeIntegrationService.IsConfigured()
            ? "Claude status-line bridge: configured"
            : "Claude status-line bridge: configuration needed";
        ResizeCards();
        ApplyTheme();
    }

    private void ResizeCards()
    {
        var width = Math.Max(320, _cards.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
        _codexCard.SetContentWidth(width);
        _claudeCard.SetContentWidth(width);
        _codexCard.Location = Point.Empty;
        _claudeCard.Location = new Point(0, _codexCard.Bottom + 12);
        _cards.AutoScrollMinSize = new Size(0, _claudeCard.Bottom);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (eventArgs.CloseReason == CloseReason.WindowsShutDown) return;
        eventArgs.Cancel = true;
        ShowInTaskbar = false;
        Hide();
    }

    private void ApplyTheme()
    {
        BackColor = ThemeColors.Background;
        ApplyColors(Controls);
    }

    private static void ApplyColors(Control.ControlCollection controls)
    {
        foreach (Control control in controls)
        {
            if (control is ProviderCard) continue;
            if (control is Button button)
            {
                button.BackColor = ThemeColors.Surface;
                button.ForeColor = ThemeColors.Foreground;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = ThemeColors.Border;
                button.FlatAppearance.MouseOverBackColor = ThemeColors.Hover;
                button.FlatAppearance.MouseDownBackColor = ThemeColors.MutedSurface;
                button.Cursor = Cursors.Hand;
                button.Padding = new Padding(10, 6, 10, 6);
            }
            else if (control is not GaugeBar)
            {
                control.BackColor = ThemeColors.Background;
                control.ForeColor = control is Label && control.Font.Bold ? ThemeColors.Foreground : ThemeColors.MutedText;
            }
            ApplyColors(control.Controls);
        }
    }
}
