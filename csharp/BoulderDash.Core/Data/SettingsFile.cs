using System.Text.Json;

namespace BoulderDash.Core.Data;

/// <summary>
/// Lädt und speichert die GameSettings als JSON. Einstellungen sind Komfort, kein Spielzustand:
/// eine fehlende, defekte oder unbeschreibbare Datei darf das Spiel nie aufhalten — in dem Fall
/// gelten die Standardwerte bzw. das Speichern entfällt stillschweigend.
/// </summary>
public static class SettingsFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>Standardablage: %APPDATA%\BoulderDash\settings.json (das Programmverzeichnis kann
    /// schreibgeschützt sein und wird von `dotnet clean` geleert).</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BoulderDash",
        "settings.json");

    public static GameSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new GameSettings();
            }

            return JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(path), Options) ?? new GameSettings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new GameSettings();
        }
    }

    public static void Save(string path, GameSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Nicht speichern zu können ist kein Grund, das Spiel zu unterbrechen.
        }
    }
}
