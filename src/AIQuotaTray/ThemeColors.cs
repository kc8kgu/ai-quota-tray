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
    public static Color Border => IsDark ? Color.FromArgb(90, 90, 95) : Color.FromArgb(210, 213, 218);
    public static Color Hover => IsDark ? Color.FromArgb(60, 60, 64) : Color.FromArgb(233, 236, 239);
    public static Color Foreground => IsDark ? Color.FromArgb(242, 242, 242) : Color.FromArgb(30, 32, 35);
    public static Color MutedText => IsDark ? Color.FromArgb(180, 180, 185) : Color.FromArgb(92, 96, 102);
    public static Color Green => Color.FromArgb(34, 160, 95);
    public static Color Amber => Color.FromArgb(221, 145, 22);
    public static Color Red => Color.FromArgb(211, 65, 65);
    public static Color Gray => Color.FromArgb(125, 130, 138);
    public static Color Blue => Color.FromArgb(58, 130, 214);

    // Per-provider accent, used for the identity dot next to each card's title and the primary action button.
    public static Color CodexAccent => Color.FromArgb(16, 163, 127);
    public static Color ClaudeAccent => Color.FromArgb(204, 120, 92);

    public static Color Gauge(double? remaining) => remaining switch
    {
        null => Gray,
        > 50 => Green,
        >= 20 => Amber,
        _ => Red
    };

    // Distinct color per connection state, so "stale" and "sign-in required" read differently at a glance
    // instead of only differing by their text.
    public static Color Status(ProviderHealthKind kind) => kind switch
    {
        ProviderHealthKind.Available => Green,
        ProviderHealthKind.Attention => Amber,
        ProviderHealthKind.Error => Red,
        ProviderHealthKind.Connecting => Blue,
        _ => Gray
    };
}

internal enum ProviderHealthKind
{
    Available,
    Connecting,
    Attention,
    Error,
    Unknown
}

