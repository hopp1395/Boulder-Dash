using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Die Amoeba. Jede Zelle würfelt pro Cave-Scan einzeln, ob sie wächst — anfangs mit 4/128 (~3 %),
/// nach Ablauf der Amoeba-Zeit mit 4/16 (25 %) — und wächst dann in genau eine zufällig gezogene der
/// vier Richtungen, sofern dort Leerraum oder Erde liegt (BDCFF 000A). Wird sie zu groß, erstarrt
/// sie zu Steinen; wird sie eingeschlossen, zerfällt sie zu Diamanten. Sie zündet Kreaturen, die sie
/// berührt.
///
/// Das DOS-Original ließ pro Scan nur eine einzige, per rand()%96 gewählte Zelle wachsen, und die
/// immer in fester Richtungspriorität (BOULDER.CPP:745-755, dort "lava" genannt).
/// </summary>
public sealed class AmoebaObject : CaveObject
{
    public override Element Element => Element.Amoeba;

    public override char MapGlyph => 'a';

    public override int DefaultFrame => 24;

    public override bool TriggersCreature => true;

    public override TileAppearance Appearance(in RenderContext ctx) =>
        TileAppearance.Of(DefaultFrame + AnimationPhase);
}
