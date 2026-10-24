using System.IO;
using System.Text.Json;

namespace TaskdeckOverlay.Core;

/// <summary>User settings stored as JSON in %APPDATA%\taskdeck-overlay\settings.json.</summary>
public sealed class AppSettings
{
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double Opacity { get; set; } = 1.0;
    public bool SensorsEnabled { get; set; } = true;
    // Off by default: "Stop" fires at the end of every Claude turn, so a sound each time gets noisy.
    public bool SoundEnabled { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "taskdeck-overlay",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (loaded is not null)
                {
                    loaded.Opacity = Math.Clamp(loaded.Opacity, 0.3, 1.0);
                    return loaded;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Missing or corrupt settings: fall back to defaults.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings are a convenience; failing to save must not affect the app.
        }
    }
}
