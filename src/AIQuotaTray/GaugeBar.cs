using System.Drawing.Drawing2D;

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

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pill = RoundedRect.Path(ClientRectangle, Height / 2);
        var previousClip = e.Graphics.Clip;
        e.Graphics.SetClip(pill, CombineMode.Intersect);

        using var background = new SolidBrush(ThemeColors.MutedSurface);
        e.Graphics.FillRectangle(background, ClientRectangle);
        using var fill = new SolidBrush(FillColor);
        e.Graphics.FillRectangle(fill, new Rectangle(0, 0, (int)Math.Round(Width * Value / 100d), Height));

        e.Graphics.Clip = previousClip;
    }
}

