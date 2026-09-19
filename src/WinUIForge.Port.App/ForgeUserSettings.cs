using System.Text.Json;

namespace WinUIForge.Port.App;

internal sealed class ForgeUserSettings
{
    const string SettingsFileName = "settings.json";

    public bool SourcePaneVisible { get; set; } = true;
    public bool ToolsPaneVisible { get; set; } = true;
    public double SourcePaneWidth { get; set; } = 430;
    public double ToolsPaneWidth { get; set; } = 410;
    public int ToolsTabIndex { get; set; } = 1;
    public List<string> ProjectFolders { get; set; } = new();

    public static ForgeUserSettings Load()
    {
        try
        {
            var path = SettingsPath();
            if (!File.Exists(path))
                return new ForgeUserSettings();

            var settings = JsonSerializer.Deserialize<ForgeUserSettings>(File.ReadAllText(path));
            return settings ?? new ForgeUserSettings();
        }
        catch
        {
            // A corrupt preference file must never block Forge startup.
            return new ForgeUserSettings();
        }
    }

    public void Save()
    {
        try
        {
            var path = SettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(
                    this,
                    new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Layout preferences are best-effort and must not interrupt authoring.
        }
    }

    static string SettingsPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JLA3D",
            "WinUIForge",
            SettingsFileName);
}
