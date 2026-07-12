namespace BoulderDash.Core.Simulation;

/// <summary>Was der Spieler von einer Kachel sieht (siehe <see cref="ExploreMap"/>).</summary>
public enum TileVisibility
{
    /// <summary>Nie im Blickradius gewesen — bleibt schwarz.</summary>
    Hidden,

    /// <summary>Schon erkundet, aber gerade nicht im Blickradius — blass grau.</summary>
    Explored,

    /// <summary>Im Blickradius (oder das Feature ist aus) — normale Cave-Farben.</summary>
    Visible,
}

/// <summary>
/// Cave-Explore (E-Taste): Die Cave liegt nicht mehr offen da, sondern muss erkundet werden. Nur was
/// in Rockfords Blickradius (<see cref="SightRadius"/>) liegt, ist normal zu sehen; was er schon
/// einmal gesehen hat, steht blass grau da; alles übrige bleibt schwarz.
///
/// KEINE ORIGINAL-ENTSPRECHUNG — weder das DOS-Original noch BD1 kennen so etwas. Wie die Skalierung
/// ist das eine bewusste Zutat des Ports.
///
/// Die Karte läuft parallel zur Cave mit: eine bool-Maske in CAVE-Koordinaten (wie die des
/// <see cref="ScreenCover"/>, dem diese Klasse nachgebaut ist), fortgeschrieben einmal je Tick aus
/// Rockfords Kachel-Index (GameTick). Sie ist reine DARSTELLUNG und rührt weder Physik noch Kamera an
/// — und sie zieht vor allem KEINEN Zufall: Der Zufallsstrom ist geteilt (Amoeba, Steinschub,
/// Rockfords Ruheanimation, ScreenCover), Reihenfolge und Anzahl der Ziehungen sind
/// verhaltensrelevant. Eine einzige Ziehung hier würde das Verhalten jeder Cave verschieben.
///
/// <see cref="Enabled"/> schaltet nur die DARSTELLUNG um: Die Karte wird immer fortgeschrieben, auch
/// wenn das Feature aus ist. Ein- und Ausschalten blendet den Nebel deshalb bloß ein und aus, statt
/// das Erkundete zu vergessen.
/// </summary>
public sealed class ExploreMap
{
    /// <summary>Rockfords Blickradius in Kacheln.</summary>
    public const int SightRadius = 5;

    /// <summary>
    /// Womit der quadrierte Abstand verglichen wird — und das ist NICHT <c>SightRadius²</c>.
    ///
    /// Die naheliegende Prüfung <c>dx² + dy² ≤ r²</c> rastert schlecht: Sie nimmt eine Kachel nur auf,
    /// wenn deren ECKE noch im Kreis liegt. Auf den Achsen bleiben dadurch einzelne Zacken stehen
    /// (bei r=5 ganz oben und unten je eine einzelne Kachel), und die Flanken werden flach — die Form
    /// wirkt wie ein Kreuz, nicht wie eine Scheibe.
    ///
    /// Verglichen wird deshalb mit <c>(r + 0,5)²</c>, also dem Kreis durch die MITTE der Randkacheln.
    /// Ganzzahlig ist das <c>r² + r</c> (der Rest 0,25 ändert bei ganzen Quadratzahlen nichts). Das
    /// ergibt eine runde Scheibe mit den Zeilenbreiten 5-7-9-11-11-11-11-11-9-7-5, zusammen 97 Kacheln.
    /// </summary>
    private const int SightRadiusSquared = (SightRadius * SightRadius) + SightRadius;

    private bool[] _explored = [];
    private int _width;
    private int _height;

    /// <summary>Kachel-Index, um den der Blickradius liegt: Rockford — und solange er nicht auf dem
    /// Feld steht (vor dem Eingangsaufbau, nach seinem Tod) der zuletzt bekannte Platz.</summary>
    private int _centre;

    /// <summary>Der Feature-Schalter (E-Taste). Aus heißt: alles sichtbar wie bisher.</summary>
    public bool Enabled { get; set; }

    /// <summary>Cave-Start: alles unerkundet, der Blick liegt auf dem Eingang. Damit ist dessen
    /// Umgebung schon erkundet, wenn Rockford dort herausplatzt — er startet nicht im Schwarzen.</summary>
    public void BeginCave(int width, int height, int entranceIndex)
    {
        _width = width;
        _height = height;
        _explored = new bool[width * height];
        _centre = entranceIndex;
        Explore(entranceIndex);
    }

    /// <summary>Ein Tick: Der Blick folgt Rockford und erkundet, was er sieht. <c>null</c> heißt, dass
    /// er gerade nicht auf dem Feld steht — dann bleibt der Blick, wo er war, statt zum Eingang
    /// zurückzuspringen.</summary>
    public void Reveal(int? centreIndex)
    {
        if (_explored.Length == 0)
        {
            return;
        }

        if (centreIndex is { } centre)
        {
            _centre = centre;
        }

        Explore(_centre);
    }

    public TileVisibility Visibility(int x, int y)
    {
        if (!Enabled || _explored.Length == 0)
        {
            return TileVisibility.Visible;
        }

        if (IsInSight(x, y, _centre % _width, _centre / _width))
        {
            return TileVisibility.Visible;
        }

        return _explored[(y * _width) + x] ? TileVisibility.Explored : TileVisibility.Hidden;
    }

    /// <summary>Merkt alle Kacheln im Blickradius um <paramref name="centreIndex"/> als erkundet.</summary>
    private void Explore(int centreIndex)
    {
        var centreX = centreIndex % _width;
        var centreY = centreIndex / _width;

        var top = Math.Max(0, centreY - SightRadius);
        var bottom = Math.Min(_height - 1, centreY + SightRadius);
        var left = Math.Max(0, centreX - SightRadius);
        var right = Math.Min(_width - 1, centreX + SightRadius);

        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                if (IsInSight(x, y, centreX, centreY))
                {
                    _explored[(y * _width) + x] = true;
                }
            }
        }
    }

    private static bool IsInSight(int x, int y, int centreX, int centreY)
    {
        var dx = x - centreX;
        var dy = y - centreY;
        return (dx * dx) + (dy * dy) <= SightRadiusSquared;
    }
}
