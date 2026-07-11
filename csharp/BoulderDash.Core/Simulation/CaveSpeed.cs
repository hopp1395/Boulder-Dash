namespace BoulderDash.Core.Simulation;

/// <summary>
/// Spieltempo einer geladenen Cave nach BD1 (C64) — Tempo-Quelle des Ports, steht als GameSpeed an
/// der CaveData (eine CaveData ist genau eine Cave auf genau einem Schwierigkeitsgrad).
///
/// In BD1 ist das Tempo NICHT pro Cave gesetzt (anders als im DOS-Original, das dafür ein
/// game_speed-Byte im Level-Header hatte, BOULDER1.CPP:1126), sondern hängt allein am
/// Schwierigkeitsgrad: pro Cave-Scan wartet BD1 in einer Verzögerungsschleife 90 Zyklen je Einheit
/// CaveDelay (C64 = 1 MHz), mit CaveDelay 12/6/3/1/0 für die Grade 1..5.
///
/// Die daraus folgenden Scan-Zeiten sind reverse-engineert als
/// "88 ms + 3,66 ms * CaveDelay + Element-/Animationsanteil" (Intermissions: Basis 60 ms statt
/// 88 ms); für Cave A/1 wurden ~150 ms pro Scan gemessen. Der Element-/Animationsanteil hängt im
/// Original vom Cave-Inhalt ab (eine Cave voller Steine scannt länger als eine voller Erde) und
/// schwankt daher von Scan zu Scan; er ist hier bewusst als Konstante (~18 ms) eingemittelt und
/// wird nicht simuliert. Quellen: Peter Broadribb, "Inside BoulderDash"
/// (elmerproductions.com/sp/peterb/insideBoulderdash.html) und der Thread "Speeds in Boulder
/// Dash 1" (boulder-dash.nl/forum) mit der GDash-Formel und Messwerten.
///
/// Umgesetzt wird das Tempo über die Tickrate: die Physik läuft alle 3 Ticks (Clk1-Periode, siehe
/// Clocks/GameTick), ein Tick dauert also ein Drittel einer Scan-Periode. Damit skalieren auch
/// Animation, Kamera-Scroll, Eingangsaufbau und ScreenCover mit — wie in BD1, wo all das am
/// Cave-Scan hängt. Nur die Spielsekunde darf NICHT mitskalieren: sie zählt in BD1 IRQ-getrieben
/// (~64 IRQ-Ticks statt 60, also länger als eine echte Sekunde) und ist damit tempo-unabhängig.
/// Deshalb führt <see cref="GameSecondTicks"/> die clk_18-Periode nach, sodass eine Spielsekunde
/// immer <see cref="GameSecondSeconds"/> reale Sekunden dauert.
/// </summary>
public readonly record struct CaveSpeed
{
    /// <summary>Reale Dauer einer Spielsekunde, tempo-unabhängig. Entspricht der Original-clk_18-
    /// Periode 22 bei 20 Hz (BOULDER.CPP:227) und liegt damit — wie von BD1 gefordert — über einer
    /// echten Sekunde (BD1: ~64/60 s).</summary>
    public const double GameSecondSeconds = 1.1;

    /// <summary>Ticks pro Cave-Scan = Clk1-Periode (Clocks): die Physik läuft bei Clk1 == 0.</summary>
    private const int TicksPerScan = 3;

    /// <summary>Dauer eines Cave-Scans in Millisekunden, Grad 1..5, reguläre Caves
    /// (aus CaveDelay 12/6/3/1/0 abgeleitet, siehe Klassenkommentar).</summary>
    private static readonly double[] ScanMillisecondsNormal = [150, 128, 117, 110, 106];

    /// <summary>Dasselbe für Intermissions (kleinere Basis: 60 ms statt 88 ms).</summary>
    private static readonly double[] ScanMillisecondsIntermission = [88, 76, 70, 64, 61];

    private CaveSpeed(double secondsPerScan) => SecondsPerScan = secondsPerScan;

    /// <summary>Dauer eines Cave-Scans (ein regel()-Durchlauf) in Sekunden.</summary>
    public double SecondsPerScan { get; }

    /// <summary>Dauer eines Ticks (eines Timer-ISR-Durchlaufs) in Sekunden.</summary>
    public double SecondsPerTick => SecondsPerScan / TicksPerScan;

    /// <summary>Ticks, die eine Spielsekunde dauert — die clk_18-Periode für dieses Tempo.
    /// So gewählt, dass eine Spielsekunde bei jedem Tempo rund <see cref="GameSecondSeconds"/>
    /// reale Sekunden dauert (Rundung auf ganze Ticks: Abweichung bleibt unter 3 %).</summary>
    public int GameSecondTicks =>
        (int)Math.Round(GameSecondSeconds / SecondsPerTick, MidpointRounding.AwayFromZero);

    /// <summary>Tempo aus der Scan-Dauer, wie sie als GameSpeed in der Cave-Datei steht (WYSIWYG:
    /// was dort steht, wird gespielt).</summary>
    public static CaveSpeed FromScanMilliseconds(double scanMilliseconds) =>
        scanMilliseconds > 0
            ? new CaveSpeed(scanMilliseconds / 1000.0)
            : throw new ArgumentOutOfRangeException(nameof(scanMilliseconds), scanMilliseconds, "Scan-Dauer muss > 0 sein.");

    /// <summary>Das BD1-Soll-Tempo für einen Schwierigkeitsgrad 1..5 und die Cave-Art. Wird zum
    /// Laden NICHT benutzt — das Tempo steht in der Cave-Datei — sondern ist die Herleitung, gegen
    /// die die ausgelieferten Cave-Dateien im Test geprüft werden.</summary>
    public static CaveSpeed For(int level, bool isIntermission)
    {
        if (level is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Schwierigkeitsgrad muss 1..5 sein.");
        }

        var table = isIntermission ? ScanMillisecondsIntermission : ScanMillisecondsNormal;
        return new CaveSpeed(table[level - 1] / 1000.0);
    }
}
