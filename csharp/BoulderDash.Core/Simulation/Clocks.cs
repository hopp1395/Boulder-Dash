namespace BoulderDash.Core.Simulation;

/// <summary>
/// Tick-Zähler aus der Game-ISR (src/BOULDER.CPP:224-227). Die Post-Inkrement-Vergleiche
/// (`if (clk++>N) clk=0`) ergeben Periode N+2: Clk1 Periode 3, Clk4 Periode 6, Clk18 Periode 22.
/// clk_2 aus dem Original wird nicht repliziert, da im aktiven Code nirgends gelesen (toter Zähler).
/// </summary>
public sealed class Clocks
{
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

        if (Clk18++ > 20)
        {
            Clk18 = 0;
        }
    }

    /// <summary>Wie die Nullung aller clk_* in level_laden (BOULDER.CPP:984-987).</summary>
    public void Reset()
    {
        Clk1 = 0;
        Clk4 = 0;
        Clk18 = 0;
    }
}
