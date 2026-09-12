using Microsoft.Win32;

namespace AIQuotaTray;

internal static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AIQuotaTray";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return string.Equals(key?.GetValue(ValueName) as string, StartupCommand(), StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue(ValueName, StartupCommand());
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }

    private static string StartupCommand() => $"\"{Application.ExecutablePath}\" --startup";
}
