using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class SettingsFileTests
{
    /// <summary>Legt einen eigenen Pfad in einem Wegwerf-Verzeichnis an (die Datei selbst wird von
    /// den Tests erst erzeugt).</summary>
    private static string NewTempPath() =>
        Path.Combine(Path.GetTempPath(), $"boulderdash-test-{Guid.NewGuid():N}", "settings.json");

    /// <summary>Erster Start (noch keine Einstellungsdatei): Vollbild mit der vollen Cave — der
    /// größtmögliche sichtbare Bereich.</summary>
    [Fact]
    public void Fehlende_Datei_liefert_Vollbild_mit_voller_Cave()
    {
        var settings = SettingsFile.Load(NewTempPath());

        Assert.Equal(ViewportSize.Full, settings.Viewport);
        Assert.Equal(ViewportSize.Steps[^1], settings.Viewport); // die größte Zoomstufe
        Assert.True(settings.Fullscreen);
        Assert.Equal(GameSettings.DefaultWindowWidth, settings.WindowWidth);
        Assert.Equal(GameSettings.DefaultWindowHeight, settings.WindowHeight);
    }

    [Fact]
    public void Defekte_Datei_liefert_Standardwerte()
    {
        var path = NewTempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(path, "{ das ist kein JSON");

            var settings = SettingsFile.Load(path);

            Assert.Equal(ViewportSize.Full, settings.Viewport);
            Assert.Equal(GameSettings.DefaultWindowWidth, settings.WindowWidth);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void Einstellungen_ueberleben_Speichern_und_Laden()
    {
        var path = NewTempPath();
        try
        {
            var saved = GameSettings.From(new ViewportSize(32, 18), 1280, 800, fullscreen: true);
            SettingsFile.Save(path, saved);

            var loaded = SettingsFile.Load(path);

            Assert.Equal(new ViewportSize(32, 18), loaded.Viewport);
            Assert.Equal(1280, loaded.WindowWidth);
            Assert.Equal(800, loaded.WindowHeight);
            Assert.True(loaded.Fullscreen);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    /// <summary>Eine von Hand verstellte Sichtfenstergröße darf keine krumme Zwischengröße ergeben.</summary>
    [Fact]
    public void Krumme_Sichtfenstergroesse_wird_auf_eine_Stufe_gerundet()
    {
        var path = NewTempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(path, """{ "ViewportColumns": 26, "ViewportRows": 15 }""");

            var settings = SettingsFile.Load(path);

            Assert.Equal(new ViewportSize(24, 14), settings.Viewport);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
