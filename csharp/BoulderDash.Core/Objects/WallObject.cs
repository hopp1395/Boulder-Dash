using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>Gemauerte Wand. Abgerundet (BDCFF 0000): Steine und Diamanten rollen von ihr ab, wenn
/// daneben Platz ist. Sprengbar.</summary>
public sealed class WallObject : CaveObject
{
    public WallObject(Cave? cave = null)
        : base(cave)
    {
    }

    public override Element Element => Element.Wall;

    public override char MapGlyph => 'w';

    public override int DefaultFrame => 11;

    public override bool IsRounded => true;
}
