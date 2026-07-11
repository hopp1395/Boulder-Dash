namespace BoulderDash.Core.Simulation;

/// <summary>
/// Tick-Zähler aus der Game-ISR (src/BOULDER.CPP:224-227). Die Post-Inkrement-Vergleiche
/// (`if (clk++>N) clk=0`) ergeben Periode N+2: Clk1 Periode 3, Clk4 Periode 6, Clk18 Periode 22.
/// clk_2 aus dem Original wird nicht repliziert, da im aktiven Code nirgends gelesen (toter Zähler).
///
/// Clk18 (der Sekundentakt) ist als einziger Zähler einstellbar: seine Periode hängt am Spieltempo,
/// weil eine Spielsekunde tempo-unabhängig gleich lang bleiben muss (siehe CaveSpeed). Der Default
/// 22 ist die Original-Periode bei 20 Hz.
/// </summary>
public sealed class Clocks
{
    /// <summary>clk_18-Periode des Originals (20 Hz -> Spielsekunde 1,1 s).</summary>
    public const int DefaultGameSecondTicks = 22;

    private int _gameSecondTicks = DefaultGameSecondTicks;

    public byte Clk1 { get; private set; }
    public byte Clk4 { get; private set; }
    public byte Clk18 { get; private set; }

    public void Tick()
    {
        if (Clk1++ > 1)
        {
            Clk1 = 0;
        }

        if (Clk4++ > 4)
        {
            Clk4 = 0;
        }

        // Modulo statt der Postfix-Form des Originals, weil die Periode hier variabel ist —
        // für 22 zählt es identisch zu `if (clk_18++ > 20) clk_18 = 0;` (Werte 0..21).
        Clk18 = (byte)((Clk18 + 1) % _gameSecondTicks);
    }

    /// <summary>Wie die Nullung aller clk_* in level_laden (BOULDER.CPP:984-987).</summary>
    public void Reset() => Reset(DefaultGameSecondTicks);

    /// <summary>Nullung wie <see cref="Reset()"/>, dazu die Clk18-Periode für das Tempo der
    /// geladenen Cave (CaveSpeed.GameSecondTicks).</summary>
    public void Reset(int gameSecondTicks)
    {
        if (gameSecondTicks < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(gameSecondTicks), gameSecondTicks, "Clk18-Periode muss >= 2 sein.");
        }

        _gameSecondTicks = gameSecondTicks;
        Clk1 = 0;
        Clk4 = 0;
        Clk18 = 0;
    }
}
