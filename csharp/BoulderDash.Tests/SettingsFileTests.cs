using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class SettingsFileTests
{
    /// <summary>Legt einen eigenen Pfad in einem Wegwerf-Verzeichnis an (die Datei selbst wird von
    /// den Tests erst erzeugt).</summary>
    private static string NewTempPath() =>
        Path.Combine(Path.GetTempPath(), $"boulderdash-test-{Guid.NewGuid():N}", "settings.json");

    /// <summary>Erster Start (noch keine Einstellungsdatei): Vollbild mit der vollen BD1-Cave. Nicht
    /// die größte Zoomstufe (die zeigt beim nativen Maßstab weit mehr als eine Cave), sondern die, bei
    /// der eine Original-Cave genau hineinpasst und nicht mehr scrollt.</summary>
    [Fact]
    public void Fehlende_Datei_liefert_Vollbild_mit_voller_Cave()
    {
        var settings = SettingsFile.Load(NewTempPath());

        Assert.Equal(ViewportSize.Full, settings.Viewport);
        Assert.Equal(new ViewportSize(40, 22), settings.Viewport);
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
            var saved = GameSettings.From(new ViewportSize(32, 18), 1280, 800, fullscreen: true, explore: true);
            SettingsFile.Save(path, saved);

            var loaded = SettingsFile.Load(path);

            Assert.Equal(new ViewportSize(32, 18), loaded.Viewport);
            Assert.Equal(1280, loaded.WindowWidth);
            Assert.Equal(800, loaded.WindowHeight);
            Assert.True(loaded.Fullscreen);
            Assert.True(loaded.Explore);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    /// <summary>Die gemerkte Sichtfenstergröße ist nur ein Wunschwert: Welche Stufen es gibt, hängt am
    /// Bildschirm (ViewportSteps) und steht erst in der Schale fest — die Einstellungen reichen den
    /// Wert deshalb unverändert durch, gerundet wird dort (siehe ViewportStepsTests.Snap...).</summary>
    [Fact]
    public void Sichtfenstergroesse_wird_unveraendert_durchgereicht()
    {
        var path = NewTempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(path, """{ "ViewportColumns": 26, "ViewportRows": 15 }""");

            var settings = SettingsFile.Load(path);

            Assert.Equal(new ViewportSize(26, 15), settings.Viewport);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
