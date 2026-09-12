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

        using var background = new SolidBrush(Color.FromArgb(45, 48, 52));
        using var fill = new SolidBrush(ThemeColors.Gauge(remaining));
        using var outline = new Pen(Color.White, 2);
        graphics.FillEllipse(background, 2, 2, 28, 28);

        var sweep = remaining is null ? 0f : (float)(360d * remaining.Value / 100d);
        if (sweep > 0) graphics.FillPie(fill, 5, 5, 22, 22, -90, sweep);
        graphics.DrawEllipse(outline, 3, 3, 26, 26);
        using var font = new Font("Segoe UI", 7, FontStyle.Bold);
        graphics.DrawString("AI", font, Brushes.White, new PointF(9, 10));

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
