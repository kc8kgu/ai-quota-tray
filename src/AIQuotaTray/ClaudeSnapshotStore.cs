using AIQuotaTray.Core;

namespace AIQuotaTray;

internal static class ClaudeSnapshotStore
{
    private const string MutexName = "Local\\AIQuotaTray.ClaudeSnapshot";

    public static void WriteLatest(ProviderSnapshot incoming)
    {
        using var mutex = new Mutex(false, MutexName);
        var acquired = false;
        try
        {
            try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new IOException("Timed out while updating the Claude usage snapshot.");

            var existing = AtomicJsonStore.Read<ProviderSnapshot>(AppPaths.ClaudeSnapshotFile);
            if (SnapshotRules.ShouldReplace(existing, incoming))
            {
                AtomicJsonStore.Write(AppPaths.ClaudeSnapshotFile, incoming);
            }
        }
        finally
        {
            if (acquired) mutex.ReleaseMutex();
        }
    }
}
