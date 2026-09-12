using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIQuotaTray;

internal static class ClaudeIntegrationService
{
    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");

    public static bool IsConfigured()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return false;
            var root = JsonNode.Parse(File.ReadAllText(SettingsPath));
            var command = root?["statusLine"]?["command"]?.GetValue<string>();
            if (command?.Contains("--claude-statusline", StringComparison.OrdinalIgnoreCase) != true) return false;

            // A command naming a program that no longer exists renders nothing and writes no snapshot, so
            // the settings entry alone is not proof: the bridge counts as configured only while the
            // executable it points at is still on disk. Checking that here is what lets startup notice a
            // moved or renamed install and rewrite the command instead of reporting a bridge that is dead.
            return BridgeExecutable(command) is { } executable && File.Exists(executable);
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return false;
        }
    }

    /// Pulls the program out of a status-line command line. Configure writes the path quoted, so the
    /// unquoted branch is a best effort for a hand-edited entry.
    internal static string? BridgeExecutable(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            return end > 1 ? command[1..end] : null;
        }

        var space = command.IndexOf(' ');
        return space > 0 ? command[..space] : null;
    }

    public static void Configure()
    {
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);

        JsonObject root;
        if (File.Exists(SettingsPath))
        {
            var original = File.ReadAllText(SettingsPath);
            root = JsonNode.Parse(original)?.AsObject() ?? new JsonObject();
            var backup = SettingsPath + ".aiquotatray.backup";
            if (!File.Exists(backup)) File.WriteAllText(backup, original);
        }
        else
        {
            root = new JsonObject();
        }

        root["statusLine"] = new JsonObject
        {
            ["type"] = "command",
            ["command"] = $"\"{Application.ExecutablePath}\" --claude-statusline",
            ["padding"] = 1
        };

        AtomicJsonStore.Write(SettingsPath, root);
    }

    public static void Remove()
    {
        if (!File.Exists(SettingsPath)) return;
        var root = JsonNode.Parse(File.ReadAllText(SettingsPath))?.AsObject() ?? new JsonObject();
        var command = root["statusLine"]?["command"]?.GetValue<string>();
        if (command?.Contains("--claude-statusline", StringComparison.OrdinalIgnoreCase) != true) return;

        var backupPath = SettingsPath + ".aiquotatray.backup";
        JsonNode? previousStatusLine = null;
        if (File.Exists(backupPath))
        {
            previousStatusLine = JsonNode.Parse(File.ReadAllText(backupPath))?["statusLine"]?.DeepClone();
        }

        if (previousStatusLine is null) root.Remove("statusLine");
        else root["statusLine"] = previousStatusLine;
        AtomicJsonStore.Write(SettingsPath, root);
    }
}
