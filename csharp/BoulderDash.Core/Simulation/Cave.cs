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

    /// <summary>Startrichtung "unten" eines frisch geladenen Butterfly (Richtungsbits 0x60).</summary>
    private const byte ButterflyStartRaw = 0x60 | (byte)Element.Butterfly;

    public Cave(CaveData data)
    {
        Width = data.Width;
        Height = data.Height;
        _tiles = (byte[])data.Tiles.Clone();

        // Butterflies starten nach BD1 nach UNTEN blickend, nicht nach links (BDCFF-Objektspezifikation
        // 0009, elmerproductions.com/sp/peterb/BDCFF/objects/0009.html: "butterflies usually begin life
        // facing down rather than left"). Weil sie ihre Vorzugsrichtung im Uhrzeigersinn suchen, ist ihr
        // erster Zug damit der nach links. Die Kartendaten kennen nur die Element-ID, die Richtungsbits
        // sind dort immer 0 (= links) — deshalb wird die Startrichtung hier beim Aufbau des
        // Simulationsgitters gesetzt. Fireflies starten korrekt nach links und bleiben unverändert.
        for (var i = 0; i < _tiles.Length; i++)
        {
            if (_tiles[i] == (byte)Element.Butterfly)
            {
                _tiles[i] = ButterflyStartRaw;
            }
        }
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
