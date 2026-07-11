using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Data;

public enum DrawKind
{
    Point,
    Line,
    FilledRect,
    Rect,
}

/// <summary>Richtung einer Linie, Reihenfolge und dx/dy exakt wie im BD1-Rohformat (Code 0-7).</summary>
public enum Bd1Direction
{
    Up,
    UpRight,
    Right,
    DownRight,
    Down,
    DownLeft,
    Left,
    UpLeft,
}

public static class Bd1DirectionOffsets
{
    private static readonly int[] Dx = [0, 1, 1, 1, 0, -1, -1, -1];
    private static readonly int[] Dy = [-1, -1, 0, 1, 1, 1, 0, -1];

    public static (int Dx, int Dy) Of(Bd1Direction direction) => (Dx[(int)direction], Dy[(int)direction]);
}

/// <summary>Ein Eintrag der Zufallsfüllungs-Tabelle (Cave-Header 0x18-0x1F): Objekt = Wahrscheinlichkeit
/// (0-255). Mehrere Einträge werden der Reihe nach geprüft, spätere haben Vorrang.</summary>
public readonly record struct RandomFillEntry(Element Element, byte Probability);

/// <summary>Ein Zeichenbefehl aus dem Cave-Rohformat (ab Header-Byte 0x20), Cave-Koordinaten
/// (Rohdaten-Zeile bereits um den C64-Bildschirm-Offset -2 korrigiert).</summary>
public readonly record struct DrawCommand(
    DrawKind Kind,
    Element Element,
    int X,
    int Y,
    int Length = 0,
    Bd1Direction Direction = Bd1Direction.Up,
    int Width = 0,
    int Height = 0,
    Element FillElement = Element.Empty);

/// <summary>
/// Geparste Cave-Textdatei: BD1-Rohformat (Header-Werte + Zufallsfüllung + Zeichenbefehle),
/// noch nicht zu einer Kachelkarte aufgebaut (siehe CaveMapBuilder.Build).
/// </summary>
public sealed class CaveDefinition
{
    public required Cave Cave { get; init; }
    public required CaveLevel Level { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required bool IsIntermission { get; init; }
    public required byte Width { get; init; }
    public required byte Height { get; init; }
    public required byte RandomSeed { get; init; }
    public required byte JewelsNeeded { get; init; }
    public required byte CaveTime { get; init; }
    public required byte MagicWallTime { get; init; }
    public required byte JewelValue { get; init; }
    public required byte JewelValueExtra { get; init; }

    /// <summary>Die 3 rohen C64-Farbindizes aus dem Cave-Header (Bytes 0x13-0x15).</summary>
    public required byte[] Colors { get; init; }

    public required IReadOnlyList<RandomFillEntry> RandomFill { get; init; }
    public required IReadOnlyList<DrawCommand> DrawCommands { get; init; }
}
