namespace BoulderDash.Core.Simulation;

/// <summary>
/// Größe des Kamera-Sichtfensters in Kacheln. Das Original kannte nur eine Größe (20x12 Kacheln =
/// 320x200 VGA-Pixel abzüglich der Statuszeile, src/BOULDER.CPP:78); der Port erlaubt darüber
/// hinaus, die Spielfläche stufenweise bis auf die volle Cave-Größe (40x22) aufzuziehen, sodass
/// gar nicht mehr gescrollt werden muss.
///
/// Die Scroll-Geometrie (Auslöser und Scrollweiten, siehe RockfordObject.ScrollCamera) leitet sich
/// aus der Sichtfenstergröße ab und ergibt bei <see cref="Original"/> exakt die Originalwerte.
/// </summary>
public readonly record struct ViewportSize(int Columns, int Rows)
{
    /// <summary>Das Sichtfenster des Originals — die Referenzgröße für alles Verhalten.</summary>
    public static readonly ViewportSize Original = new(20, 12);

    /// <summary>Die ganze Cave auf einmal — die größte Stufe, bei der gar nicht mehr gescrollt wird.
    /// Voreinstellung beim ersten Start (siehe GameSettings).</summary>
    public static readonly ViewportSize Full = new(40, 22);

    /// <summary>Wählbare Stufen: vom Original bis zur vollen Cave (40x22), je +4 Spalten/+2 Zeilen.
    /// Bewusst keine Stufe unter dem Original: die Statuszeile ist genau 20 Kacheln (320 px) breit,
    /// und ein kleineres Bild liefert schon der Bildschirm-Zoom (Fenstergröße).</summary>
    public static readonly IReadOnlyList<ViewportSize> Steps =
    [
        new(20, 12),
        new(24, 14),
        new(28, 16),
        new(32, 18),
        new(36, 20),
        new(40, 22),
    ];

    /// <summary>Auslöser für den Rechts-Scroll: Rockford überschreitet diese Spalte im Sichtfenster.
    /// Original: 16 bei 20 Spalten (BOULDER.CPP:893, hier eine Kachel weiter innen).</summary>
    public int ScrollTriggerRight => Columns - 4;

    /// <summary>Auslöser für den Abwärts-Scroll. Original: 8 bei 12 Zeilen (BOULDER.CPP:895).</summary>
    public int ScrollTriggerBottom => Rows - 4;

    /// <summary>Auslöser für Links-/Aufwärts-Scroll: im Original ein fester Rand (BOULDER.CPP:894,896).</summary>
    public const int ScrollTriggerNear = 2;

    /// <summary>Scrollweite waagerecht; rückt Rockford wieder etwa in die Fenstermitte.
    /// Original: 7 bei 20 Spalten.</summary>
    public int ScrollAmountX => (Columns / 2) - 3;

    /// <summary>Scrollweite senkrecht. Original: 5 bei 12 Zeilen.</summary>
    public int ScrollAmountY => (Rows / 2) - 1;

    public ViewportSize NextLarger() => StepAt(IndexOfNearestStep() + 1);

    public ViewportSize NextSmaller() => StepAt(IndexOfNearestStep() - 1);

    /// <summary>Nächstgelegene gültige Stufe — fängt beliebige (z. B. aus einer alten oder
    /// handgeschriebenen Einstellungsdatei gelesene) Werte ab.</summary>
    public static ViewportSize Snap(int columns, int rows)
    {
        var best = Steps[0];
        var bestDistance = int.MaxValue;
        foreach (var step in Steps)
        {
            var distance = Math.Abs(step.Columns - columns) + Math.Abs(step.Rows - rows);
            if (distance < bestDistance)
            {
                best = step;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static ViewportSize StepAt(int index) => Steps[Math.Clamp(index, 0, Steps.Count - 1)];

    private int IndexOfNearestStep()
    {
        var snapped = Snap(Columns, Rows);
        for (var i = 0; i < Steps.Count; i++)
        {
            if (Steps[i] == snapped)
            {
                return i;
            }
        }

        return 0;
    }
}
