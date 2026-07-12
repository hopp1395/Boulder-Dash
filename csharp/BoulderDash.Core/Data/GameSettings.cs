using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Data;

/// <summary>
/// Benutzereinstellungen der Spielschale (kein Spielzustand): Spielflächen-Zoom (Sichtfenstergröße)
/// und Bildschirm-Zoom (Fenstergröße bzw. Vollbild). Werden beim Start geladen und bei Änderung
/// gespeichert, siehe SettingsFile.
/// </summary>
public sealed record GameSettings
{
    /// <summary>Fenstergröße beim ersten Start: das Original-Sichtfenster (320x200) dreifach
    /// vergrößert — die bisherige feste Größe des Ports.</summary>
    public const int DefaultWindowWidth = 960;
    public const int DefaultWindowHeight = 600;

    public int ViewportColumns { get; init; } = ViewportSize.Original.Columns;
    public int ViewportRows { get; init; } = ViewportSize.Original.Rows;
    public int WindowWidth { get; init; } = DefaultWindowWidth;
    public int WindowHeight { get; init; } = DefaultWindowHeight;
    public bool Fullscreen { get; init; }

    /// <summary>Sichtfenstergröße als Stufe — fängt krumme Werte aus einer von Hand bearbeiteten
    /// Einstellungsdatei ab (siehe ViewportSize.Snap).</summary>
    public ViewportSize Viewport => ViewportSize.Snap(ViewportColumns, ViewportRows);

    public static GameSettings From(ViewportSize viewport, int windowWidth, int windowHeight, bool fullscreen) => new()
    {
        ViewportColumns = viewport.Columns,
        ViewportRows = viewport.Rows,
        WindowWidth = Math.Max(1, windowWidth),
        WindowHeight = Math.Max(1, windowHeight),
        Fullscreen = fullscreen,
    };
}
