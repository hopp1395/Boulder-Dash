using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Die Amoeba. Jede Zelle würfelt pro Cave-Scan einzeln, ob sie wächst — anfangs mit 4/128 (~3 %),
/// nach Ablauf der Amoeba-Zeit mit 4/16 (25 %) — und wächst dann in genau eine zufällig gezogene der
/// vier Richtungen, sofern dort Leerraum oder Erde liegt (BDCFF 000A). Sie zündet Kreaturen, die sie
/// berührt.
///
/// Ihr Schicksal hängt an ihrer GESAMTHEIT: Wird sie zu groß, erstarrt sie zu Steinen; wird sie
/// eingeschlossen, zerfällt sie zu Diamanten. Beides kann eine einzelne Zelle nicht sehen — sie zählt
/// sich dafür in der Objekt-Auflistung der Cave selbst durch (<see cref="TakeCensus"/>). Gemessen
/// wird am Ende jedes Scans; gewirkt wird erst im FOLGENDEN, denn beide Regeln greifen laut
/// Spezifikation eine Generation später ("zu groß" hat dabei Vorrang).
///
/// Das DOS-Original ließ pro Scan nur eine einzige, per rand()%96 gewählte Zelle wachsen, und die
/// immer in fester Richtungspriorität (BOULDER.CPP:745-755, dort "lava" genannt).
/// </summary>
public sealed class AmoebaObject : CaveObject
{
    /// <summary>Ab dieser Zellzahl erstarrt die ganze Amoeba zu Steinen. Feste BD1-Konstante (in BD1
    /// nicht pro Cave einstellbar); das DOS-Original wandelte schon ab 96 Zellen um (BOULDER.CPP:951).</summary>
    public const int MaxSize = 200;

    public AmoebaObject(Cave cave)
        : base(cave)
    {
    }

    public override Element Element => Element.Amoeba;

    public override char MapGlyph => 'a';

    public override int DefaultFrame => 24;

    public override bool TriggersCreature => true;

    public override TileAppearance Appearance(in RenderContext ctx) =>
        TileAppearance.Of(DefaultFrame + AnimationPhase);

    /// <summary>Ob diese Zelle irgendwo Platz zum Wachsen hat.</summary>
    public bool CanGrow
    {
        get
        {
            var width = Cave.Width;
            foreach (var direction in (ReadOnlySpan<int>)[-width, width, -1, 1])
            {
                if (CanEat(Neighbour(direction)))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Wozu die Amoeba zerfällt — oder null, wenn sie weiterwächst. Maßgeblich sind die Zahlen des
    /// VORIGEN Scans (<see cref="TakeCensus"/> schreibt sie erst an dessen Ende), sie stehen also für
    /// den ganzen laufenden Scan fest: Alle Zellen teilen dasselbe Schicksal, egal wann der Scan sie
    /// besucht. "Zu groß" hat Vorrang vor "eingeschlossen".
    /// </summary>
    private Element? Fate => Cave.State.AmoebaCountLastScan >= MaxSize
        ? Element.Boulder
        : Cave.State.AmoebaSuffocatedLastScan ? Element.Jewel : null;

    /// <summary>
    /// Die Amoeba vermisst sich selbst: Sie zählt ihre Zellen in der Objekt-Auflistung der Cave und
    /// merkt sich Größe und Platznot für den Folge-Scan. Die Cave ruft das einmal am Ende jedes Scans
    /// auf — nur sie weiß, wann ein Scan zu Ende ist.
    ///
    /// Gezählt wird VOR dem Zurücksetzen der Verarbeitet-Flags, und zwar aus einem Grund: Frisch
    /// gewachsene Zellen tragen das Flag noch und zählen bewusst nicht mit ("newly grown amoeba
    /// cannot expand until the following frame"). Danach wäre die Unterscheidung verloren, und die
    /// Amoeba risse die 200er-Schwelle eine Generation zu früh.
    /// </summary>
    public static void TakeCensus(Cave cave)
    {
        var size = 0;
        var canGrow = false;

        foreach (var tile in cave.Objects)
        {
            if (tile is not AmoebaObject { ScannedThisFrame: false } cell)
            {
                continue;
            }

            size++;
            canGrow |= cell.CanGrow;
        }

        var state = cave.State;
        state.AmoebaCountLastScan = size;
        state.AmoebaSuffocatedLastScan = size > 0 && !canGrow;

        // Für die Amoeba-Drone (kein Original-Äquivalent, siehe AudioPlayer/SoundRecipes).
        state.AmoebaPresent = size > 0;
    }

    public override void Interact()
    {
        if (ScannedThisFrame)
        {
            return;
        }

        if (Fate is { } fate)
        {
            // Mit Verarbeitet-Flag, damit ein so entstandener Stein nicht schon im selben Scan
            // weiterfällt (dasselbe Muster wie bei der auslaufenden Explosion).
            var converted = Cave.Create(fate);
            converted.ScannedThisFrame = true;
            Cave.Spawn(Index, converted);
            return;
        }

        var width = Cave.Width;
        ReadOnlySpan<int> directions = [-width, width, -1, 1];

        var slowly = Cave.State.AmoebaSlowGrowthRemaining > 0;
        if (Cave.Random.Next(slowly ? 128 : 16) >= 4)
        {
            return;
        }

        var target = Index + directions[Cave.Random.Next(directions.Length)];
        if (CanEat(Cave.Get(target)))
        {
            // Mit Verarbeitet-Flag: Sie wächst erst im nächsten Scan weiter und zählt auch erst dann
            // mit ("newly grown amoeba cannot expand until the following frame").
            Cave.Spawn(target, new AmoebaObject(Cave) { ScannedThisFrame = true });
        }
    }

    /// <summary>
    /// Der Speiseplan der Amoeba: Sie frisst sich in Leerraum und Erde hinein — an allem anderen
    /// (Wände, Steine, Diamanten, Kreaturen) prallt sie ab. Im Original war das die wiederkehrende
    /// Prüfung "(x &amp; 0xFE) == 0", die genau die Element-IDs 0 und 1 trifft.
    ///
    /// Eine Kachel, die in diesem Scan schon angefasst wurde, ist tabu — sonst würde sich die Amoeba
    /// in eine Zelle fressen, die gerade erst freigeräumt wurde, und im selben Scan der eigenen
    /// Bewegung hinterherwachsen.
    ///
    /// Dass die Amoeba weiß, was sie frisst (und nicht die Erde, dass sie gefressen wird), ist
    /// Absicht: Der Speiseplan gehört zum Fressenden.
    /// </summary>
    private static bool CanEat(CaveObject target) =>
        target is EmptyObject or EarthObject && !target.ScannedThisFrame;
}
