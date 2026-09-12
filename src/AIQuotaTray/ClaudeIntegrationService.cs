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
            return root?["statusLine"]?["command"]?.GetValue<string>()?.Contains(
                "--claude-statusline", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return false;
        }
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
