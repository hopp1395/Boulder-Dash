using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>Erde. Rockford gräbt sich hindurch, die Amoeba wächst hinein — sonst passiert nichts.</summary>
public sealed class EarthObject : CaveObject
{
    public EarthObject(Cave cave)
        : base(cave)
    {
    }

    public override Element Element => Element.Earth;

    public override char MapGlyph => '.';

    public override int DefaultFrame => 1;
}
