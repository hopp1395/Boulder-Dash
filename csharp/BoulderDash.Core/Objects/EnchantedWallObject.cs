using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Zaubermauer. Sieht aus wie eine gewöhnliche Mauer, bis der erste Stein auf sie fällt — dann läuft
/// sie an, mahlt sichtbar und wandelt für die Dauer der Umwandlungszeit jedes fallende Objekt in
/// sein Gegenstück um, das zwei Zeilen tiefer wieder herauskommt (siehe FallingObject).
/// Nicht abgerundet und sprengbar.
/// </summary>
public sealed class EnchantedWallObject : CaveObject
{
    public override Element Element => Element.EnchantedWall;

    public override char MapGlyph => 'M';

    /// <summary>Wie die Mauer, bis die Umwandlung sie anlaufen lässt.</summary>
    public override int DefaultFrame => 11;

    public override TileAppearance Appearance(in RenderContext ctx) => ctx.EnchantedWallRunning
        ? TileAppearance.Of(60 + AnimationPhase)
        : TileAppearance.Of(DefaultFrame);
}
