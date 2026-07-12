using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Stein. Rockford kann ihn waagerecht schieben — aber nur, wenn er ruht ("he cannot push falling
/// boulders", BDCFF 0006) und nur mit einer Chance von 1:8 pro Versuch.
/// </summary>
public sealed class BoulderObject : FallingObject
{
    public override Element Element => Element.Boulder;

    public override char MapGlyph => 'r';

    public override int DefaultFrame => 2;

    public override SoundEvent LandingSound => SoundEvent.BoulderLand;

    public override FallingObject EnchantedWallProduct() => new JewelObject { Falling = true };
}
