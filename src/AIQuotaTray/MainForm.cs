using AIQuotaTray.Core;

namespace AIQuotaTray;

internal sealed class MainForm : Form
{
    private const string RefreshIdleText = "Refresh now";
    private const string RefreshBusyText = "Refreshing…";

    private readonly ProviderCard _codexCard = new("Codex", ThemeColors.CodexAccent);
    private readonly ProviderCard _claudeCard = new("Claude", ThemeColors.ClaudeAccent);
    private readonly Panel _cards = new();
    private readonly Button _refresh = new() { Text = RefreshIdleText, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8), Tag = "primary" };
    public event EventHandler? RefreshRequested;
    public event EventHandler? CodexLoginRequested;
    public event EventHandler? OpenClaudeRequested;

    public MainForm()
    {
        Text = "AI Quota Tray";
        ClientSize = new Size(560, 720);
        MinimumSize = new Size(520, 640);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 4
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var heading = new Label
        {
            Text = "AI Subscription Usage",
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

        var actions = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, 12, 0, 0),
            Padding = new Padding(0),
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        _refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        var signIn = new Button { Text = "Sign in to Codex", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 4, 0) };
        signIn.Click += (_, _) => CodexLoginRequested?.Invoke(this, EventArgs.Empty);
        var openClaude = new Button { Text = "Open Claude Code", Dock = DockStyle.Fill, Margin = new Padding(4, 0, 0, 0) };
        openClaude.Click += (_, _) => OpenClaudeRequested?.Invoke(this, EventArgs.Empty);
        actions.Controls.Add(_refresh, 0, 0);
        actions.SetColumnSpan(_refresh, 2);
        actions.Controls.Add(signIn, 0, 1);
        actions.Controls.Add(openClaude, 1, 1);

        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(subtitle, 0, 1);
        root.Controls.Add(_cards, 0, 2);
        root.Controls.Add(actions, 0, 3);
        Controls.Add(root);

        FormClosing += OnFormClosing;
        ApplyTheme();
    }

    /// A refresh can finish without changing a single displayed value, so the button itself carries the
    /// only proof that the click was heard.
    public void SetRefreshing(bool refreshing)
    {
        _refresh.Enabled = !refreshing;
        _refresh.Text = refreshing ? RefreshBusyText : RefreshIdleText;
    }

    public void UpdateSnapshot(ApplicationSnapshot snapshot, DateTimeOffset now)
    {
        _codexCard.UpdateSnapshot(snapshot.Codex, now);
        _claudeCard.UpdateSnapshot(snapshot.Claude, now);
        ResizeCards();
        ApplyTheme();
    }

    private void ResizeCards()
    {
        var width = AvailableCardWidth();
        ResizeCardsTo(width);
        _cards.PerformLayout();

        // Adding the cards can make the vertical scrollbar appear. Re-evaluate once so the cards
        // reserve the gutter only when it is real, keeping the outer margins visually symmetric.
        var settledWidth = AvailableCardWidth();
        if (settledWidth != width) ResizeCardsTo(settledWidth);
    }

    private int AvailableCardWidth() => Math.Max(320,
        _cards.ClientSize.Width - (_cards.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0));

    private void ResizeCardsTo(int width)
    {
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
                var isPrimary = button.Tag as string == "primary";
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = isPrimary ? 0 : 1;
                button.FlatAppearance.BorderColor = ThemeColors.Border;
                button.Cursor = Cursors.Hand;
                button.Padding = new Padding(12, 7, 12, 7);
                if (isPrimary)
                {
                    button.BackColor = ThemeColors.Blue;
                    button.ForeColor = Color.White;
                    button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(ThemeColors.Blue, 0.15f);
                    button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(ThemeColors.Blue, 0.1f);
                }
                else
                {
                    button.BackColor = ThemeColors.Surface;
                    button.ForeColor = ThemeColors.Foreground;
                    button.FlatAppearance.MouseOverBackColor = ThemeColors.Hover;
                    button.FlatAppearance.MouseDownBackColor = ThemeColors.MutedSurface;
                }
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
