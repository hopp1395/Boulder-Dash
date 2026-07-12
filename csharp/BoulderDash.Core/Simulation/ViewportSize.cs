namespace BoulderDash.Core.Simulation;

/// <summary>
/// Größe des Kamera-Sichtfensters in Kacheln. Das Original kannte nur eine Größe (20x12 Kacheln =
/// 320x200 VGA-Pixel abzüglich der Statuszeile, src/BOULDER.CPP:78); der Port erlaubt darüber hinaus,
/// die Spielfläche stufenweise aufzuziehen — welche Größen es dabei überhaupt gibt, hängt an der
/// Zeichenfläche und wird deshalb nicht hier festgelegt, sondern in <see cref="ViewportSteps"/>.
///
/// Die Scroll-Geometrie (Auslöser und Scrollweiten, siehe RockfordObject.ScrollCamera) leitet sich
/// aus der Sichtfenstergröße ab und ergibt bei <see cref="Original"/> exakt die Originalwerte.
/// </summary>
public readonly record struct ViewportSize(int Columns, int Rows)
{
    /// <summary>Das Sichtfenster des Originals — die Referenzgröße für alles Verhalten und zugleich
    /// die kleinste Zoomstufe (siehe ViewportSteps).</summary>
    public static readonly ViewportSize Original = new(20, 12);

    /// <summary>Die ganze BD1-Cave auf einmal — bei ihr scrollt eine Original-Cave nicht mehr.
    /// Der Wunsch-Zoom beim ersten Start (siehe GameSettings); ob es diese Stufe gibt, entscheidet
    /// der Bildschirm (auf 1920x1080 im Vollbild ist sie genau der Maßstab 3x).</summary>
    public static readonly ViewportSize Full = new(40, 22);

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
}
