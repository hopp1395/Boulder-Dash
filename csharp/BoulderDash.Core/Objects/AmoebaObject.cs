using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Die Amoeba. Jede Zelle würfelt pro Cave-Scan einzeln, ob sie wächst — anfangs mit 4/128 (~3 %),
/// nach Ablauf der Amoeba-Zeit mit 4/16 (25 %) — und wächst dann in genau eine zufällig gezogene der
/// vier Richtungen, sofern dort Leerraum oder Erde liegt (BDCFF 000A). Sie zündet Kreaturen, die sie
/// berührt.
///
/// Ihr Schicksal entscheidet sie nicht allein: Wird sie zu groß, erstarrt sie zu Steinen; wird sie
/// eingeschlossen, zerfällt sie zu Diamanten. Beides hängt an der Zählung der GESAMTEN Amoeba und
/// wirkt erst im Folge-Scan — deshalb meldet sich jede Zelle bei der Cave (Cave.ReportAmoeba) und
/// fragt dort ihr Schicksal ab (Cave.AmoebaFate).
///
/// Das DOS-Original ließ pro Scan nur eine einzige, per rand()%96 gewählte Zelle wachsen, und die
/// immer in fester Richtungspriorität (BOULDER.CPP:745-755, dort "lava" genannt).
/// </summary>
public sealed class AmoebaObject : CaveObject
{
    public AmoebaObject(Cave? cave = null)
        : base(cave)
    {
    }

    public override Element Element => Element.Amoeba;

    public override char MapGlyph => 'a';

    public override int DefaultFrame => 24;

    public override bool TriggersCreature => true;

    public override TileAppearance Appearance(in RenderContext ctx) =>
        TileAppearance.Of(DefaultFrame + AnimationPhase);

    public override void NextState()
    {
        if (Scanned)
        {
            return;
        }

        if (Cave.AmoebaFate is { } fate)
        {
            // Mit Verarbeitet-Flag, damit ein so entstandener Stein nicht schon im selben Scan
            // weiterfällt (dasselbe Muster wie bei der auslaufenden Explosion).
            var converted = Cave.Create(fate);
            converted.Scanned = true;
            Cave.Spawn(Index, converted);
            return;
        }

        var width = Cave.Width;
        ReadOnlySpan<int> directions = [-width, width, -1, 1];

        var canGrow = false;
        foreach (var direction in directions)
        {
            if (Neighbour(direction).CanAmoebaGrowInto)
            {
                canGrow = true;
                break;
            }
        }

        Cave.ReportAmoeba(canGrow);

        var slowly = Cave.State.AmoebaSlowGrowthRemaining > 0;
        if (Cave.Random.Next(slowly ? 128 : 16) >= 4)
        {
            return;
        }

        var target = Index + directions[Cave.Random.Next(directions.Length)];
        if (Cave.Get(target).CanAmoebaGrowInto)
        {
            // Mit Verarbeitet-Flag: Sie wächst erst im nächsten Scan weiter und zählt auch erst dann
            // mit ("newly grown amoeba cannot expand until the following frame").
            Cave.Spawn(target, new AmoebaObject(Cave) { Scanned = true });
        }
    }
}
