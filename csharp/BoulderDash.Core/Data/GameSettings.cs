using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Data;

/// <summary>
/// Benutzereinstellungen der Spielschale (kein Spielzustand): Spielflächen-Zoom (Sichtfenstergröße)
/// und Bildschirm-Zoom (Fenstergröße bzw. Vollbild). Werden beim Start geladen und bei Änderung
/// gespeichert, siehe SettingsFile.
/// </summary>
public sealed record GameSettings
{
    /// <summary>Fenstergröße beim ersten Start: die volle Cave (40x22 Kacheln = 640x360 logische
    /// Pixel) doppelt vergrößert. Greift erst, wenn der Spieler das voreingestellte Vollbild per
    /// F11 verlässt.</summary>
    public const int DefaultWindowWidth = 1280;
    public const int DefaultWindowHeight = 720;

    /// <summary>Ohne Einstellungsdatei (erster Start) zeigt das Spiel die volle Cave im Vollbild —
    /// also den größtmöglichen sichtbaren Bereich. Beide Zooms bleiben danach frei änderbar (+/-, F11)
    /// und werden ab dann gemerkt.</summary>
    public int ViewportColumns { get; init; } = ViewportSize.Full.Columns;
    public int ViewportRows { get; init; } = ViewportSize.Full.Rows;
    public int WindowWidth { get; init; } = DefaultWindowWidth;
    public int WindowHeight { get; init; } = DefaultWindowHeight;
    public bool Fullscreen { get; init; } = true;

    /// <summary>Cave-Explore (E-Taste im Spiel): Die Cave muss erkundet werden, siehe ExploreMap.
    /// Ohne Einstellungsdatei — und in jeder älteren ohne dieses Feld — ist das Feature aus.</summary>
    public bool Explore { get; init; }

    /// <summary>Sichtfenstergröße als Stufe — fängt krumme Werte aus einer von Hand bearbeiteten
    /// Einstellungsdatei ab (siehe ViewportSize.Snap).</summary>
    public ViewportSize Viewport => ViewportSize.Snap(ViewportColumns, ViewportRows);

    public static GameSettings From(ViewportSize viewport, int windowWidth, int windowHeight, bool fullscreen, bool explore) => new()
    {
        ViewportColumns = viewport.Columns,
        ViewportRows = viewport.Rows,
        WindowWidth = Math.Max(1, windowWidth),
        WindowHeight = Math.Max(1, windowHeight),
        Fullscreen = fullscreen,
        Explore = explore,
    };
}
