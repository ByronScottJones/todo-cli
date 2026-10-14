using System.Text.Json;
using System.Text.Json.Nodes;

namespace Todo.Tui;

/// <summary>
/// Per-user settings the UI can change (currently only the theme). They live in a small JSON file that is read as an
/// optional configuration source after <c>appsettings.json</c>, so a choice made in the UI persists without editing
/// files that may be read-only or shared.
/// </summary>
public sealed class UserSettingsStore
{
    private readonly string _path;

    public UserSettingsStore(string path)
    {
        _path = path;
    }

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "todo", "settings.json");

    /// <summary>Sets <c>Tui:Theme</c>, preserving every other setting already in the file.</summary>
    public void SaveTheme(string themeName)
    {
        var root = ReadExisting();
        if (root["Tui"] is not JsonObject tui)
        {
            tui = new JsonObject();
            root["Tui"] = tui;
        }

        tui["Theme"] = themeName;

        var directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Write to a temp file and move it into place so a crash cannot leave a half-written settings file.
        var temp = _path + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _path, overwrite: true);
    }

    private JsonObject ReadExisting()
    {
        if (!File.Exists(_path))
            return new JsonObject();

        try
        {
            return JsonNode.Parse(File.ReadAllText(_path)) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            // A corrupt file is replaced rather than blocking the user from saving a choice.
            return new JsonObject();
        }
    }
}
