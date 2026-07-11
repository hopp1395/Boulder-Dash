using BoulderDash.Core.Data;

namespace BoulderDash.Core.Simulation;

/// <summary>
/// Veränderliches Simulationsgitter einer Cave (Kopie von CaveData.Tiles). Kachelbytes bleiben
/// bewusst roh: Bits 0-3 Element-ID, Bit 0x80 "in diesem Sweep verarbeitet", Bit 0x40
/// Fall-Momentum, Bits 0x60/0x70 Kreaturen-Richtungszustand (siehe CavePhysics). Kein Zerlegen
/// in separate Flag-Felder, da die Original-Masken quer durch diese Bitgruppen schneiden.
/// </summary>
public sealed class Cave
{
    private readonly byte[] _tiles;

    public int Width { get; }
    public int Height { get; }

    public Cave(CaveData data)
    {
        Width = data.Width;
        Height = data.Height;
        _tiles = (byte[])data.Tiles.Clone();
    }

    public int IndexOf(int x, int y) => (y * Width) + x;

    public byte GetRaw(int index) => _tiles[index];

    public void SetRaw(int index, byte value) => _tiles[index] = value;

    public Element GetElement(int index) => (Element)(_tiles[index] & 0x0F);

    public Element GetElement(int x, int y) => GetElement(IndexOf(x, y));

    /// <summary>Erste Kachel mit dem angegebenen Element (zeilenweise), wie level_laden (BOULDER.CPP:1032-1035).</summary>
    public int FindFirstIndexOf(Element element)
    {
        var raw = (byte)element;
        for (var i = 0; i < _tiles.Length; i++)
        {
            if (_tiles[i] == raw)
            {
                return i;
            }
        }

        return -1;
    }
}
