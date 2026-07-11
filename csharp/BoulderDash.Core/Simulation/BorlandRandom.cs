namespace BoulderDash.Core.Simulation;

/// <summary>
/// Nachbildung von Borlands C-Laufzeit-rand() (ohne srand()-Aufruf, Standard-Seed 1).
/// level_in() (Dissolve) und regel() (Lava-Ausbreitung) teilen sich im Original EINE globale
/// Sequenz — daher hier eine Instanz pro Spielsitzung, geteilt zwischen Dissolve und CavePhysics.
/// </summary>
public sealed class BorlandRandom
{
    private uint _seed;

    public BorlandRandom(uint seed = 1)
    {
        _seed = seed;
    }

    /// <summary>Liefert einen Wert im Bereich 0..32767, wie C rand().</summary>
    public int Next()
    {
        _seed = (_seed * 0x015A4E35u) + 1;
        return (int)((_seed >> 16) & 0x7FFF);
    }
}
