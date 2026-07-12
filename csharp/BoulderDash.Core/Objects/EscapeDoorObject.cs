using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Der Ausgang. Bis die Diamantenquote erfüllt ist, ist er von einer Stahlwand nicht zu
/// unterscheiden — genau so tarnt ihn auch das Original ("buffer[MASK_HAUSA]=buffer[MASK_STAHL]",
/// BOULDER.CPP:1000). Danach schaltet ihn ende() frei und er blinkt wie der Eingang. Unzerstörbar.
/// </summary>
public sealed class EscapeDoorObject : CaveObject
{
    public EscapeDoorObject(Cave cave)
        : base(cave)
    {
    }

    public override Element Element => Element.EscapeDoor;

    public override char MapGlyph => 'X';

    /// <summary>Der Stahlwand-Frame — die Tarnung, siehe Klassenkommentar.</summary>
    public override int DefaultFrame => 12;

    /// <summary>Hält der Explosion stand: In BD1 ist der Ausgang eine Stahlwand-Variante — er sieht
    /// bis zu seiner Freischaltung ja auch aus wie Stahl.</summary>
    public override void Detonate(Func<ExplosionObject> create)
    {
    }

    public override TileAppearance Appearance(in RenderContext ctx) => ctx.ExitFlashOn
        ? TileAppearance.Of(ctx.Clk4 < 3 ? 49 : 48)
        : TileAppearance.Of(DefaultFrame);
}
