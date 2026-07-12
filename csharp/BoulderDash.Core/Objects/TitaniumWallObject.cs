using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>Stahlwand — unzerstörbar und nicht abgerundet. Sie umschließt jede Cave.</summary>
public sealed class TitaniumWallObject : CaveObject
{
    public TitaniumWallObject(Cave cave)
        : base(cave)
    {
    }

    public override Element Element => Element.TitaniumWall;

    public override char MapGlyph => 'W';

    public override int DefaultFrame => 12;

    public override bool IsExplosionProof => true;
}
