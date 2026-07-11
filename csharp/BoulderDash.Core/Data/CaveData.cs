using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Data;

/// <summary>
/// Eine geladene Cave: Header-Werte plus Kachelkarte, aufgebaut aus der BD1-Rohformat-Textdatei
/// (siehe CaveMapBuilder). Die Kacheln sind hier noch roh (reine Element-IDs ohne Verarbeitungs-/
/// Bewegungs-Flags), wie unmittelbar nach dem Aufbau.
/// </summary>
public sealed class CaveData
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required char Letter { get; init; }
    public required bool IsIntermission { get; init; }
    public required byte Width { get; init; }
    public required byte Height { get; init; }
    public required byte JewelQuota { get; init; }
    public required byte TimeSeconds { get; init; }
    public required byte[] BaseColors { get; init; }
    public required byte CameraStartX { get; init; }
    public required byte CameraStartY { get; init; }
    public required byte EnchantedWallSeconds { get; init; }
    public required byte PointsPerJewelBeforeQuota { get; init; }
    public required byte PointsPerJewelAfterQuota { get; init; }
    public required byte GameSpeed { get; init; }

    /// <summary>Kachelkarte, zeilenweise, Länge = Width*Height, ein Byte = eine Element-ID.</summary>
    public required byte[] Tiles { get; init; }

    public Element GetElement(int x, int y) => (Element)(Tiles[y * Width + x] & 0x0F);

    /// <summary>
    /// Sucht die erste Kachel mit dem angegebenen Element (zeilenweise, wie die
    /// Original-Suchschleifen für Ein-/Ausgang in level_laden, BOULDER.CPP:1032-1035).
    /// </summary>
    public int FindFirstIndexOf(Element element)
    {
        var raw = (byte)element;
        for (var i = 0; i < Tiles.Length; i++)
        {
            if (Tiles[i] == raw)
            {
                return i;
            }
        }

        return -1;
    }
}
