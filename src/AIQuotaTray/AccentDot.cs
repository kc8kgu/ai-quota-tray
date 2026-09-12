using System.Drawing.Drawing2D;

namespace AIQuotaTray;

/// <summary>Small filled circle used to give each provider a recognizable color accent next to its name.</summary>
internal sealed class AccentDot : Control
{
    private Color _fillColor = Color.Gray;

    public Color FillColor
    {
        get => _fillColor;
        set { _fillColor = value; Invalidate(); }
    }

    public AccentDot()
    {
        Size = new Size(10, 10);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Margin = new Padding(0, 4, 8, 0);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width <= 0 || Height <= 0) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(FillColor);
        e.Graphics.FillEllipse(brush, 0, 0, Width - 1, Height - 1);
    }
}
