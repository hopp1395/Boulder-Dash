using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>Die Explosion eines Schmetterlings: Sie hinterlässt einen Diamanten statt Leerraum.
/// Deshalb sind Schmetterlinge in vielen Caves die einzige Quelle der Diamantenquote.</summary>
public sealed class JewelExplosionObject : ExplosionObject
{
    public JewelExplosionObject()
    {
        // Der Schmetterling sprengt im Original mit 0xCE - Bit 0x40 gesetzt (siehe CausedByCreature).
        CausedByCreature = true;
    }

    public override Element Element => Element.JewelExplosion;

    public override int DefaultFrame => 68;

    public override CaveObject Remnant() => new JewelObject { Scanned = true };
}
