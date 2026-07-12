namespace BoulderDash.Core.Simulation;

/// <summary>
/// Die Leiter der Zoomstufen des Spielflächen-Zooms (+/-) — abgeleitet aus der Zeichenfläche, auf
/// der das Bild landet (Fenster bzw. Bildschirm im Vollbild).
///
/// Der Zoom ist NICHT eine feste Liste von Sichtfenstergrößen, sondern der <b>Kachelmaßstab</b>:
/// eine Stufe ist „eine Kachel ist 16*n Bildschirmpixel groß“, und das Sichtfenster ist schlicht,
/// was bei diesem Maßstab auf die Fläche passt. Nur so geht die Rechnung immer auf — eine feste
/// Größenliste trifft die Auflösung bloß zufällig, und wo sie danebenliegt (80x44 = 1280x712 auf
/// 1920x1080 = Faktor 1,5), fällt der ganzzahlige Maßstab auf den nächstkleineren zurück: Die
/// Kacheln schrumpfen sprunghaft, der halbe Schirm bleibt schwarz, und zwei Stufen können identisch
/// aussehen. Aus der Fläche abgeleitet ist dagegen jede Stufe pixelscharf und füllt den Schirm.
///
/// Die Enden der Leiter:
/// <list type="bullet">
/// <item>unten fest das Original-Sichtfenster (<see cref="ViewportSize.Original"/>, 20x12) — kleiner
/// wird das Spielfeld nie, ein kleineres Bild liefert der Bildschirm-Zoom (Fenstergröße);</item>
/// <item>oben der native Maßstab 1x — eine Kachel ist dann 16 echte Bildschirmpixel groß. Mehr
/// Kacheln gingen nur noch durch Herunterrechnen, und das kostet die scharfen Pixel.</item>
/// </list>
///
/// Auf 1920x1080 im Vollbild ergibt das 20x12 (5x), 24x13 (5x), 30x16 (4x), 40x22 (3x), 60x33 (2x)
/// und 120x67 (1x) — die volle BD1-Cave (40x22) fällt dabei genau auf den Maßstab 3x.
/// </summary>
public sealed class ViewportSteps
{
    private readonly List<ViewportSize> _sizes;

    private ViewportSteps(List<ViewportSize> sizes)
    {
        _sizes = sizes;
    }

    /// <summary>Die Stufen, aufsteigend: die kleinste (das Original) zuerst, die native zuletzt.</summary>
    public IReadOnlyList<ViewportSize> Sizes => _sizes;

    /// <summary>
    /// Leitet die Stufen für eine Zeichenfläche ab. <paramref name="tileSize"/> und
    /// <paramref name="statusLineHeight"/> sind die Pixelmaße der Zeichenschicht
    /// (CaveRenderer.TileSize/StatusLineHeight) — Core kennt sie nicht von sich aus.
    /// </summary>
    public static ViewportSteps For(int surfaceWidth, int surfaceHeight, int tileSize, int statusLineHeight)
    {
        var sizes = new List<ViewportSize>();

        // Vom nativen Maßstab (1x, größtes Sichtfenster) nach oben, bis die Stufe das Original
        // unterschreitet. Beide Maße schrumpfen mit wachsendem Maßstab, der Abbruch ist also endgültig.
        for (var scale = 1; ; scale++)
        {
            var columns = surfaceWidth / (tileSize * scale);
            var rows = ((surfaceHeight / scale) - statusLineHeight) / tileSize;

            if (columns <= ViewportSize.Original.Columns || rows <= ViewportSize.Original.Rows)
            {
                break;
            }

            sizes.Add(new ViewportSize(columns, rows));
        }

        // Das Original ist immer die unterste Stufe — auch auf einer Fläche, die für nichts anderes
        // reicht (dann ist es die einzige, und der Bildschirm-Zoom rechnet es passend herunter).
        sizes.Add(ViewportSize.Original);
        sizes.Reverse();

        return new ViewportSteps(sizes);
    }

    /// <summary>Eine Stufe hinaus (größeres Sichtfenster, mehr Kacheln); am oberen Ende bleibt es stehen.</summary>
    public ViewportSize Larger(ViewportSize current) => At(IndexOf(current) + 1);

    /// <summary>Eine Stufe hinein (kleineres Sichtfenster, größere Kacheln); am unteren Ende bleibt es stehen.</summary>
    public ViewportSize Smaller(ViewportSize current) => At(IndexOf(current) - 1);

    /// <summary>Nächstgelegene Stufe — fängt Größen ab, die es auf dieser Fläche nicht (mehr) gibt:
    /// den Wunschwert aus der Einstellungsdatei, oder die bisherige Stufe, nachdem das Fenster
    /// gezogen oder ins Vollbild geschaltet wurde.</summary>
    public ViewportSize Snap(ViewportSize viewport) => _sizes[IndexOf(viewport)];

    private ViewportSize At(int index) => _sizes[Math.Clamp(index, 0, _sizes.Count - 1)];

    private int IndexOf(ViewportSize viewport)
    {
        var best = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < _sizes.Count; i++)
        {
            var distance = Math.Abs(_sizes[i].Columns - viewport.Columns)
                + Math.Abs(_sizes[i].Rows - viewport.Rows);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }
}
