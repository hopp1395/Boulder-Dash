using BoulderDash.Core.Data;
using BoulderDash.Core.Objects;

namespace BoulderDash.Core.Simulation;

/// <summary>
/// Die Höhle: das Gitter aus <see cref="CaveObject"/>s und zugleich die Welt, in der sie leben.
///
/// Die Objekte rechnen ihren Zustand selbst aus — jedes kennt seine Cave und seinen Platz darin und
/// kann deshalb seine Nachbarn ansehen, sich bewegen und andere sprengen. Die Cave gibt ihnen dazu
/// nur den Takt: <see cref="NextFrame"/> einmal je Tick für die Animation, <see cref="NextState"/>
/// einmal je Cave-Scan für die Physik (das Äquivalent von regel(), src/BOULDER.CPP:725-959). Die
/// Spielregeln stehen bei den Objekten, nicht hier.
///
/// Damit ein Objekt das kann, hält die Cave, was der ganzen Höhle gehört und keiner einzelnen
/// Kachel: <see cref="State"/> (Punkte, Quote, Restzeiten, Sound-Ereignisse), <see cref="Input"/>
/// (Rockfords Steuerung), <see cref="Camera"/> (die Scroll-Auslöser) und <see cref="Random"/>.
///
/// Die Scan-Richtung ist verhaltensrelevant: Weil zeilenweise von oben nach unten gescannt wird,
/// würde ein fallendes Objekt seiner eigenen Bewegung hinterherfallen — das verhindert das
/// Verarbeitet-Flag (<see cref="CaveObject.ScannedThisFrame"/>), das jedes Objekt beim Zug setzt und das der
/// Scan am Ende wieder löscht.
///
/// <see cref="CaveData"/> bleibt bewusst roh (byte[]): Es ist der unveränderliche Dateiinhalt und
/// wird über mehrere Spieldurchläufe hinweg wiederverwendet.
/// </summary>
public sealed class Cave
{
    private readonly CaveObject[] _tiles;

    public int Width { get; }
    public int Height { get; }

    /// <summary>Alle Objekte der Höhle, zeilenweise. Damit kann ein Objekt Fragen beantworten, die
    /// über seine Nachbarschaft hinausgehen — die Amoeba etwa zählt hier ihre eigene Gesamtgröße
    /// (siehe AmoebaObject.TakeCensus).</summary>
    public IEnumerable<CaveObject> Objects => _tiles;

    /// <summary>Fortschritt der laufenden Cave — was der Höhle gehört, nicht einer Kachel.</summary>
    public GameState State { get; }

    public InputState Input { get; }

    public Camera Camera { get; }

    /// <summary>Der gemeinsame Zufallsstrom (Amoeba-Wachstum, Steinschub). Fester Seed, siehe
    /// GameSession — Reihenfolge UND Anzahl der Ziehungen sind verhaltensrelevant.</summary>
    public Random Random { get; }

    /// <summary>Der Animationstakt der Höhle (früher der globale Zähler wechsel_vier): Alle Objekte
    /// laufen in ihm, und frisch entstehende übernehmen ihn, damit sie nicht aus der Reihe tanzen.</summary>
    public byte AnimationPhase { get; private set; }

    public Cave(CaveData data, GameState state, InputState input, Camera camera, Random random)
    {
        Width = data.Width;
        Height = data.Height;
        State = state;
        Input = input;
        Camera = camera;
        Random = random;

        _tiles = new CaveObject[data.Tiles.Length];
        for (var i = 0; i < _tiles.Length; i++)
        {
            var tile = CaveObjects.FromRaw(this, data.Tiles[i]);
            tile.Index = i;
            _tiles[i] = tile;
        }
    }

    public int IndexOf(int x, int y) => (y * Width) + x;

    public CaveObject Get(int index) => _tiles[index];

    public CaveObject Get(int x, int y) => _tiles[IndexOf(x, y)];

    /// <summary>Legt ein Objekt auf eine Kachel und sagt ihm, wo es nun steht. Für ein Objekt, das
    /// sich BEWEGT — es behält seine Instanz und damit seinen ganzen Zustand.</summary>
    public void Set(int index, CaveObject value)
    {
        value.Index = index;
        _tiles[index] = value;
    }

    /// <summary>Wie <see cref="Set"/>, aber für ein NEU entstandenes Objekt: Es übernimmt zusätzlich
    /// die aktuelle Animationsphase der Höhle, damit es im Gleichtakt mit allen übrigen animiert.</summary>
    public void Spawn(int index, CaveObject value)
    {
        value.AnimationPhase = AnimationPhase;
        Set(index, value);
    }

    /// <summary>Ein neues Objekt für diese Höhle (mit ihr als Welt und ihrer Animationsphase).</summary>
    public CaveObject Create(Element element) => CaveObjects.Create(this, element, AnimationPhase);

    /// <summary>Ein Animationsschritt für jedes Objekt — einmal pro Tick
    /// (sprites_wechsel()/boulder_lauf(), BOULDER.CPP:593-646).</summary>
    public void NextFrame()
    {
        AnimationPhase = (byte)((AnimationPhase + 1) % CaveObject.AnimationPeriod);

        foreach (var tile in _tiles)
        {
            tile.NextFrame();
        }
    }

    /// <summary>
    /// Ein Cave-Scan: Jedes Objekt spielt sein Verhalten aus (regel(), BOULDER.CPP:725-959). Die
    /// Spielregeln stehen bei den Objekten; hier bleibt nur, was den GANZEN Scan überblicken muss und
    /// deshalb keine einzelne Kachel entscheiden kann — Rockfords Todeserkennung (er ist ja weg, wenn
    /// er stirbt) und der Zeitpunkt, zu dem die Amoeba sich vermisst.
    /// </summary>
    public void NextState()
    {
        // stat: 0, solange Rockford im Scan gefunden wurde — er selbst setzt es zurück.
        var wasAlive = State.Stat == 0;
        if (State.EntranceProgress > 100 && State.Stat == 0)
        {
            State.Stat = 1;
        }

        // Bewusst über den Index und nicht über die Objekte: Sie bewegen sich während des Scans, und
        // gelesen werden muss immer die Kachel, wie sie JETZT aussieht.
        for (var i = 0; i < _tiles.Length; i++)
        {
            _tiles[i].Interact();
        }

        if (wasAlive && State.Stat != 0)
        {
            State.SoundEvents.Enqueue(SoundEvent.Death);
        }

        // Die Amoeba vermisst sich selbst. Sie tut es hier und nicht in ihrem eigenen Zug, weil nur
        // die Höhle weiß, wann der Scan zu Ende ist — und weil noch VOR dem EndScan gezählt werden
        // muss (siehe AmoebaObject.TakeCensus).
        AmoebaObject.TakeCensus(this);

        // Jedes Objekt schließt seinen Scan ab — das Verarbeitet-Flag fällt (:930-934).
        foreach (var tile in _tiles)
        {
            tile.EndScan();
        }
    }

    /// <summary>Setzt ALLE Explosionen der Höhle auf die erste Phase zurück — auch die, die anderswo
    /// schon halb abgelaufen sind. Im Original war wechsel_explo eine einzige globale Variable, und
    /// jede neue Explosion setzte sie auf 1; alle Explosionen verschwanden dadurch gemeinsam. Diese
    /// Kopplung ist bewusst erhalten (siehe ExplosionObject).</summary>
    public void RestartExplosions()
    {
        foreach (var tile in _tiles)
        {
            if (tile is ExplosionObject explosion)
            {
                explosion.ExplosionPhase = 1;
            }
        }
    }

    /// <summary>anfang(): Eingangsaufbau — der Eingang platzt bei 92 auf, Rockford steht bei 99 da
    /// (:667-677). Die Türblinken-Animation selbst ist rein optisch und liegt beim EntranceObject.</summary>
    public void BuildEntrance(int entranceIndex)
    {
        if (State.EntranceProgress == 92)
        {
            Spawn(entranceIndex, new ExplosionObject(this));
            RestartExplosions();
            State.SoundEvents.Enqueue(SoundEvent.EntranceExplosion);
        }

        if (State.EntranceProgress == 99)
        {
            Spawn(entranceIndex, new RockfordObject(this));
        }

        State.EntranceProgress++;
    }

    /// <summary>ende(): Die Quote ist erfüllt — Palettenfarbe 0 blitzt einmal hell auf und bleibt
    /// danach dunkel, der Ausgang steht offen (:681-687).</summary>
    public void OpenEscapeDoor()
    {
        if (!State.ExitFlashOn)
        {
            State.PaletteColor0Override = Palette.ExitFlashBright;
            State.ExitFlashOn = true;
            State.SoundEvents.Enqueue(SoundEvent.EscapeDoorOpen);
        }
        else
        {
            State.PaletteColor0Override = Palette.ExitFlashDark;
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
