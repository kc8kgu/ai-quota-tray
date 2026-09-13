namespace AIQuotaTray;

internal sealed class GaugeBar : Control
{
    private double _value;
    private Color _fillColor = Color.SeaGreen;

    public double Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0, 100);
            Invalidate();
        }
    }

    public Color FillColor
    {
        get => _fillColor;
        set { _fillColor = value; Invalidate(); }
    }

    public GaugeBar()
    {
        Height = 10;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        AccessibleRole = AccessibleRole.ProgressBar;
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new GaugeAccessibleObject(this);

    private sealed class GaugeAccessibleObject(GaugeBar owner) : Control.ControlAccessibleObject(owner)
    {
        public override string Value => $"{owner.Value:0}%";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width <= 0 || Height <= 0) return;

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        using var track = RoundedRect.Path(bounds, bounds.Height / 2);
        using var background = new SolidBrush(ThemeColors.MutedSurface);
        e.Graphics.FillPath(background, track);

        var fillWidth = (int)Math.Round(bounds.Width * Value / 100d);
        if (fillWidth <= 0) return;
        using var filled = RoundedRect.Path(new Rectangle(bounds.X, bounds.Y, fillWidth, bounds.Height), bounds.Height / 2);
        using var fill = new SolidBrush(FillColor);
        e.Graphics.FillPath(fill, filled);
    }
}
