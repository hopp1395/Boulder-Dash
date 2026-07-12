namespace BoulderDash.Core.Objects;

/// <summary>
/// Was von einer Kachel zu zeichnen ist — der Zeichenauftrag eines <see cref="CaveObject"/> an die
/// Rendering-Schicht, ohne jede MonoGame-Abhängigkeit. Damit liegt die Frameauswahl in Core und ist
/// headless testbar; die Game-Schicht (SpriteAtlas) führt nur noch aus.
///
/// <see cref="Frame"/> ist ein Index in die z_zeiger-Tabelle (SpriteTables.FrameToRawSprite), nicht
/// in die Rohsprites. Die drei Sonderfälle darüber hinaus stammen alle aus dem Original:
/// <see cref="BottomFrame"/> für Rockfords zweigeteiltes Ruhebild, <see cref="RowOffset"/> für das
/// Gleitfenster im 24 Zeilen hohen Rand-Sprite und <see cref="FlipHorizontally"/> für den nach links
/// laufenden Rockford (boulder_lauf(), BOULDER.CPP:620-635).
/// </summary>
public readonly record struct TileAppearance
{
    /// <summary>Der Frame der ganzen 16x16-Kachel — oder nur ihrer oberen Hälfte, wenn
    /// <see cref="BottomFrame"/> gesetzt ist.</summary>
    public int Frame { get; init; }

    /// <summary>Gesetzt: Die Kachel wird aus zwei Hälften zusammengesetzt (<see cref="Frame"/> oben,
    /// dieser Frame unten). Nur Rockfords Ruheanimation nutzt das — auf dem C64 steuern obere und
    /// untere Körperhälfte Blinzeln und Fußtappen unabhängig voneinander.</summary>
    public int? BottomFrame { get; init; }

    /// <summary>Zeilenversatz im Quellsprite (0-7). Nur der Rand-Füllstein nutzt das: sein Sprite ist
    /// 24 statt 16 Zeilen hoch, das 16-Zeilen-Fenster wandert hindurch und lässt die Mauer nach unten
    /// durchlaufen.</summary>
    public int RowOffset { get; init; }

    /// <summary>Waagerecht gespiegelt zeichnen (Rockford läuft nach links).</summary>
    public bool FlipHorizontally { get; init; }

    public static TileAppearance Of(int frame) => new() { Frame = frame };
}
