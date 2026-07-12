using BoulderDash.Core.Data;
using BoulderDash.Core.Objects;

namespace BoulderDash.Core.Simulation;

/// <summary>
/// Veränderliches Simulationsgitter einer Cave: je Kachel ein <see cref="CaveObject"/>, aufgebaut
/// aus den Kachelbytes der geladenen Cave-Datei (CaveData bleibt bewusst roh — es ist der
/// unveränderliche Dateiinhalt und wird über mehrere Spieldurchläufe hinweg wiederverwendet).
///
/// Die Objekte tragen ihren Zustand selbst: das Verarbeitet-Flag, das Fall-Momentum eines Steins,
/// die Blickrichtung einer Kreatur, die Animationsphase. Im Original steckte all das in den Bits
/// EINES Bytes je Kachel; <see cref="CaveObject.ToRaw"/> rechnet das noch zurück, aber nur für
/// Serialisierung und Regressionsvergleiche — die Physik liest keine Bytes mehr.
/// </summary>
public sealed class Cave
{
    private readonly CaveObject[] _tiles;

    public int Width { get; }
    public int Height { get; }

    /// <summary>Der Animationstakt der Cave (wechsel_vier): Alle Objekte laufen in ihm, und frisch
    /// erzeugte übernehmen ihn beim Entstehen, damit sie nicht aus der Reihe tanzen.</summary>
    public byte AnimationPhase { get; private set; }

    public Cave(CaveData data)
    {
        Width = data.Width;
        Height = data.Height;

        _tiles = new CaveObject[data.Tiles.Length];
        for (var i = 0; i < _tiles.Length; i++)
        {
            _tiles[i] = CaveObjects.FromRaw(data.Tiles[i]);
        }
    }

    public int IndexOf(int x, int y) => (y * Width) + x;

    public CaveObject Get(int index) => _tiles[index];

    public CaveObject Get(int x, int y) => _tiles[IndexOf(x, y)];

    public void Set(int index, CaveObject value) => _tiles[index] = value;

    /// <summary>Ein neues Objekt für dieses Gitter — es übernimmt die aktuelle Cave-Animationsphase
    /// (siehe <see cref="AnimationPhase"/>).</summary>
    public CaveObject Create(Element element) => CaveObjects.Create(element, AnimationPhase);

    /// <summary>Legt ein frisch erzeugtes Objekt ins Gitter und gibt ihm die aktuelle
    /// Animationsphase mit.</summary>
    public void Spawn(int index, CaveObject value)
    {
        value.AnimationPhase = AnimationPhase;
        _tiles[index] = value;
    }

    /// <summary>Alle Objekte um einen Animationsschritt weiterschalten — einmal pro Tick. Die Cave
    /// führt den gemeinsamen Takt mit, damit neu entstehende Objekte ihn übernehmen können.</summary>
    public void Animate(InputState input)
    {
        AnimationPhase = (byte)((AnimationPhase + 1) % CaveObject.AnimationPeriod);

        foreach (var tile in _tiles)
        {
            tile.Animate(input);
        }
    }

    /// <summary>Das Verarbeitet-Flag aller Kacheln löschen — der Abschluss jedes Cave-Scans
    /// (regel(), BOULDER.CPP:930-934).</summary>
    public void ClearScanned()
    {
        foreach (var tile in _tiles)
        {
            tile.Scanned = false;
        }
    }

    /// <summary>Rockford, sofern er gerade auf dem Feld steht — vor dem Eingangsaufbau und nach
    /// seinem Tod ist er es nicht.</summary>
    public RockfordObject? FindRockford()
    {
        foreach (var tile in _tiles)
        {
            if (tile is RockfordObject rockford)
            {
                return rockford;
            }
        }

        return null;
    }

    public Element GetElement(int index) => _tiles[index].Element;

    public Element GetElement(int x, int y) => GetElement(IndexOf(x, y));

    /// <summary>Das Kachelbyte des Originals — nur noch für Serialisierung und
    /// Regressionsvergleiche (siehe <see cref="CaveObject.ToRaw"/>).</summary>
    public byte GetRaw(int index) => _tiles[index].ToRaw();

    /// <summary>Erste Kachel mit dem angegebenen Element (zeilenweise), wie level_laden
    /// (BOULDER.CPP:1032-1035).</summary>
    public int FindFirstIndexOf(Element element)
    {
        for (var i = 0; i < _tiles.Length; i++)
        {
            if (_tiles[i].Element == element)
            {
                return i;
            }
        }

        return -1;
    }
}
