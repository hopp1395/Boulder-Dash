using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>Geist: hält sich links (gegen den Uhrzeigersinn) und hinterlässt beim Explodieren
/// nichts als Leerraum. Startet nach links blickend — der Vorgabewert von CreatureFacing.</summary>
public sealed class FireflyObject : CreatureObject
{
    public override Element Element => Element.Firefly;

    public override char MapGlyph => 'F';

    public override int DefaultFrame => 32;

    public override bool PrefersCounterClockwise => true;

    /// <summary>Der Geist sprengt im Original mit 0xCC - Bit 0x40 gesetzt (siehe CausedByCreature).</summary>
    public override ExplosionObject CreateExplosion() => new() { CausedByCreature = true };

    public override TileAppearance Appearance(in RenderContext ctx) =>
        TileAppearance.Of(DefaultFrame + AnimationPhase);
}
