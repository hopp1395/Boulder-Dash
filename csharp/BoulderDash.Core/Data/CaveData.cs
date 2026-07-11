using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Data;

/// <summary>
/// Eine geladene Cave: 16-Byte-Header plus Kachelkarte, direkt aus LEVEL.BIN übernommen
/// (siehe level_laden in src/BOULDER.CPP:975-1040). Die Kacheln sind hier noch roh
/// (reine Element-IDs ohne Verarbeitungs-/Bewegungs-Flags), wie unmittelbar nach dem Laden.
/// </summary>
public sealed class CaveData
{
    public required int Index { get; init; }
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

    /// <summary>
    /// Buchstabe der Cave nach Original-Formel (GAME.CPP:39): cavenr+'A'-((cavenr-1)/4).
    /// Ganzzahldivision inkl. Original-Rundungsverhalten für negative Zwischenwerte bei Index 0.
    /// </summary>
    public char Letter
    {
        get
        {
            var cavenr = (sbyte)Index;
            var korrektur = (sbyte)((cavenr - 1) / 4);
            return (char)(cavenr + 'A' - korrektur);
        }
    }

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
