using Microsoft.Win32;

namespace AIQuotaTray;

internal static class ThemeColors
{
    public static bool IsDark
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
    }

    public static Color Background => IsDark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(248, 249, 250);
    public static Color Surface => IsDark ? Color.FromArgb(45, 45, 48) : Color.White;
    public static Color MutedSurface => IsDark ? Color.FromArgb(70, 70, 74) : Color.FromArgb(225, 228, 232);
    public static Color Foreground => IsDark ? Color.FromArgb(242, 242, 242) : Color.FromArgb(30, 32, 35);
    public static Color MutedText => IsDark ? Color.FromArgb(180, 180, 185) : Color.FromArgb(92, 96, 102);
    public static Color Green => Color.FromArgb(34, 160, 95);
    public static Color Amber => Color.FromArgb(221, 145, 22);
    public static Color Red => Color.FromArgb(211, 65, 65);
    public static Color Gray => Color.FromArgb(125, 130, 138);

    public static Color Gauge(double? remaining) => remaining switch
    {
        null => Gray,
        > 50 => Green,
        >= 20 => Amber,
        _ => Red
    };
}

