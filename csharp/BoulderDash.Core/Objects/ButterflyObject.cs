using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Schmetterling: hält sich rechts (im Uhrzeigersinn) und zerfällt beim Explodieren zu Diamanten —
/// das macht ihn zur Diamantenquelle vieler Caves.
///
/// Er startet nach UNTEN blickend, nicht nach links ("butterflies usually begin life facing down
/// rather than left", BDCFF 0009). Weil er seine Vorzugsrichtung im Uhrzeigersinn sucht, ist sein
/// erster Zug damit der nach links. Die Kartendaten kennen nur die Element-ID und keine Richtung —
/// deshalb steht die Startrichtung hier.
/// </summary>
public sealed class ButterflyObject : CreatureObject
{
    public ButterflyObject(Cave cave)
        : base(cave)
    {
        Facing = CreatureFacing.Down;
    }

    public override Element Element => Element.Butterfly;

    public override char MapGlyph => 'B';

    public override int DefaultFrame => 40;

    public override bool PrefersCounterClockwise => false;

    public override ExplosionObject CreateExplosion() => new JewelExplosionObject(Cave);

    public override TileAppearance Appearance(in RenderContext ctx) =>
        TileAppearance.Of(DefaultFrame + AnimationPhase);
}
