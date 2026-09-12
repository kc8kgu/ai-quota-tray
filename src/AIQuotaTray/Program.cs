namespace AIQuotaTray;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--claude-statusline", StringComparer.OrdinalIgnoreCase))
        {
            Environment.ExitCode = ClaudeBridge.Run();
            return;
        }

        if (args.Contains("--remove-claude-statusline", StringComparer.OrdinalIgnoreCase))
        {
            ClaudeIntegrationService.Remove();
            return;
        }

        using var singleInstance = new Mutex(true, "Local\\AIQuotaTray", out var createdNew);
        if (!createdNew) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext(args.Contains("--startup", StringComparer.OrdinalIgnoreCase)));
    }
}
