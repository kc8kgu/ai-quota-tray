namespace AIQuotaTray;

internal sealed class GaugeBar : Control
{
    private double _value;
    private Color _fillColor = Color.SeaGreen;

    public double Value
    {
        get => _value;
        set { _value = Math.Clamp(value, 0, 100); Invalidate(); }
    }

    public Color FillColor
    {
        get => _fillColor;
        set { _fillColor = value; Invalidate(); }
    }

    public GaugeBar()
    {
        Height = 8;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        AccessibleRole = AccessibleRole.ProgressBar;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var background = new SolidBrush(ThemeColors.MutedSurface);
        using var fill = new SolidBrush(FillColor);
        e.Graphics.FillRectangle(background, ClientRectangle);
        e.Graphics.FillRectangle(fill, new Rectangle(0, 0, (int)Math.Round(Width * Value / 100d), Height));
    }
}

