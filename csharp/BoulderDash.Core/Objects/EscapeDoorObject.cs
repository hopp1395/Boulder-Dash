using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Der Ausgang. Bis die Diamantenquote erfüllt ist, ist er von einer Stahlwand nicht zu
/// unterscheiden — genau so tarnt ihn auch das Original ("buffer[MASK_HAUSA]=buffer[MASK_STAHL]",
/// BOULDER.CPP:1000). Danach schaltet ihn ende() frei und er blinkt wie der Eingang. Unzerstörbar.
/// </summary>
public sealed class EscapeDoorObject : CaveObject
{
    public override Element Element => Element.EscapeDoor;

    public override char MapGlyph => 'X';

    /// <summary>Der Stahlwand-Frame — die Tarnung, siehe Klassenkommentar.</summary>
    public override int DefaultFrame => 12;

    public override bool IsExplosionProof => true;

    public override TileAppearance Appearance(in RenderContext ctx) => ctx.ExitFlashOn
        ? TileAppearance.Of(ctx.Clk4 < 3 ? 49 : 48)
        : TileAppearance.Of(DefaultFrame);
}
