namespace AIQuotaTray;

internal sealed record AppSettings(bool FirstRunComplete = false, bool StartWithWindows = true)
{
    public static AppSettings Load() => AtomicJsonStore.Read<AppSettings>(AppPaths.SettingsFile) ?? new();
    public void Save() => AtomicJsonStore.Write(AppPaths.SettingsFile, this);
}

