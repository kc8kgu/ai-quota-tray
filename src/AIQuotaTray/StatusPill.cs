using System.Drawing.Drawing2D;

namespace AIQuotaTray;

/// <summary>A small rounded badge used to give each provider's connection state a distinct color and shape,
/// instead of relying on text alone to distinguish "connected" from "stale" from "sign-in required".</summary>
internal sealed class StatusPill : Control
{
    private string _text = string.Empty;
    private Color _accent = Color.Gray;

    public string StatusText
    {
        get => _text;
        set { _text = value; UpdateSize(); Invalidate(); }
    }

    public Color Accent
    {
        get => _accent;
        set { _accent = value; Invalidate(); }
    }

    public StatusPill()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Font = new Font((SystemFonts.MessageBoxFont ?? DefaultFont).FontFamily, 8, FontStyle.Bold);
        Height = 20;
        AccessibleRole = AccessibleRole.StatusBar;
        // Position is set explicitly by ProviderCard.RepositionHeader on every resize; anchoring here as
        // well would fight that (WinForms would reposition using stale distance-from-edge math).
    }

    // A plain UserPaint control's default accessible object does not reliably surface AccessibleName to
    // screen readers (mirrors the same override GaugeBar uses) - without this, a screen reader announces
    // the pill as blank instead of "Connected" / "Stale" / etc.
    protected override AccessibleObject CreateAccessibilityInstance() => new StatusPillAccessibleObject(this);

    private sealed class StatusPillAccessibleObject(StatusPill owner) : ControlAccessibleObject(owner)
    {
        public override string? Name
        {
            get => owner.StatusText;
            set { }
        }
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdateSize();
    }

    private void UpdateSize()
    {
        var textSize = TextRenderer.MeasureText(_text, Font);
        Width = textSize.Width + 20;
        Height = textSize.Height + 8;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width <= 0 || Height <= 0) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var path = RoundedRect.Path(new Rectangle(0, 0, Width - 1, Height - 1), Height / 2);
        using var background = new SolidBrush(Color.FromArgb(28, Accent.R, Accent.G, Accent.B));
        e.Graphics.FillPath(background, path);
        using var border = new Pen(Color.FromArgb(90, Accent.R, Accent.G, Accent.B));
        e.Graphics.DrawPath(border, path);

        var textRect = ClientRectangle;
        using var textBrush = new SolidBrush(Accent);
        var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        e.Graphics.DrawString(_text, Font, textBrush, textRect, format);
    }
}
