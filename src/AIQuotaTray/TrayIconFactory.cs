using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace AIQuotaTray;

internal static class TrayIconFactory
{
    public static Icon Create(double? remaining)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        // A ring rather than a filled pie: at 16x16 (the actual taskbar size) a thick ring reads clearly as
        // "how full", where a thin pie sliver or any small text becomes an unreadable smudge.
        using var track = new Pen(Color.FromArgb(70, 255, 255, 255), 5);
        using var fill = new Pen(ThemeColors.Gauge(remaining), 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var ring = new Rectangle(4, 4, 24, 24);
        graphics.DrawEllipse(track, ring);

        var sweep = remaining is null ? 0f : (float)Math.Max(2d, 360d * remaining.Value / 100d);
        graphics.DrawArc(fill, ring, -90, sweep);

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
