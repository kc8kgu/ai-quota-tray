using System.Drawing.Drawing2D;

namespace AIQuotaTray;

internal static class RoundedRect
{
    public static GraphicsPath Path(Rectangle bounds, int radius)
    {
        radius = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
        var diameter = radius * 2;
        var path = new GraphicsPath();
        if (diameter <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            if (bounds.Width > 0 && bounds.Height > 0) path.AddRectangle(bounds);
            return path;
        }

        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
