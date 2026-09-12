namespace AIQuotaTray;

internal static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIQuotaTray");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public static string SnapshotFile => Path.Combine(DataDirectory, "snapshots.json");
    public static string ClaudeSnapshotFile => Path.Combine(DataDirectory, "claude-snapshot.json");
}

