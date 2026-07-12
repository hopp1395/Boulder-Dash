using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Diamant. Rockford sammelt ihn ein — auch im Fallen, denn eingesammelt wird über die Element-ID,
/// unabhängig vom Fall-Bit. Er funkelt im gemeinsamen Animationstakt.
/// </summary>
public sealed class JewelObject : FallingObject
{
    public override Element Element => Element.Jewel;

    public override char MapGlyph => 'd';

    public override int DefaultFrame => 3;

    public override SoundEvent LandingSound => SoundEvent.JewelLand;

    public override FallingObject EnchantedWallProduct() => new BoulderObject { Falling = true };

    public override TileAppearance Appearance(in RenderContext ctx) =>
        TileAppearance.Of(DefaultFrame + AnimationPhase);
}
