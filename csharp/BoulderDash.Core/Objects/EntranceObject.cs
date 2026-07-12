using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Der Eingang, durch den Rockford die Cave betritt. Er blinkt, solange Rockford noch nicht
/// erschienen ist; beim Eingangsaufbau sprengt ihn dann eine Explosion auf und Rockford steht da
/// (anfang(), BOULDER.CPP:667-677). Unzerstörbar — in BD1 ist er eine Stahlwand-Variante.
/// </summary>
public sealed class EntranceObject : CaveObject
{
    public EntranceObject(Cave cave)
        : base(cave)
    {
    }

    public override Element Element => Element.Entrance;

    public override char MapGlyph => 'P';

    public override int DefaultFrame => 48;

    public override bool IsExplosionProof => true;

    /// <summary>Blinkt zwischen dem Stahl- und dem Tür-Frame (48/49) im Clk4-Takt.</summary>
    public override TileAppearance Appearance(in RenderContext ctx) =>
        TileAppearance.Of(ctx.Clk4 < 3 ? 49 : 48);
}
