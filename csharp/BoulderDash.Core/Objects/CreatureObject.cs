using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Geist und Schmetterling. Beide patrouillieren an einer Wand entlang, indem sie stur ihre
/// Vorzugsdrehung versuchen: Der Geist hält sich links (gegen den Uhrzeigersinn), der Schmetterling
/// rechts. Geht das nicht, ziehen sie geradeaus; geht auch das nicht, drehen sie sich genau EINMAL
/// zur Gegenseite und bleiben in diesem Scan stehen ("if a firefly is forced to turn against its
/// 'preferred direction', it does not actually move for that frame", BDCFF 0008/0009).
///
/// Berührt sie Rockford oder die Amoeba, oder fällt ihr ein Stein auf den Kopf, explodiert sie — der
/// Geist zu Leerraum, der Schmetterling zu Diamanten.
///
/// Das DOS-Original (BOULDER.CPP:758-840) drehte stattdessen in einer Schleife bis zu viermal weiter
/// und unterdrückte die Bewegung über ein Extra-Bit 0x10. Die Bewegung stimmte damit zwar, die
/// resultierende BLICKRICHTUNG einer blockierten Kreatur aber nicht — und der Schmetterling drehte
/// bei Blockade sogar auf seine Vorzugsseite statt auf die Gegenseite. Beide Kreaturen teilen sich
/// hier denselben Code: Sie folgen jeder Regel gemeinsam und unterscheiden sich nur in Drehsinn und
/// Explosionsart.
/// </summary>
public abstract class CreatureObject : CaveObject
{
    protected CreatureObject(Cave? cave)
        : base(cave)
    {
    }

    public CreatureFacing Facing { get; set; }

    /// <summary>Wohin sich die Kreatur bevorzugt dreht: der Geist gegen, der Schmetterling im
    /// Uhrzeigersinn.</summary>
    public abstract bool PrefersCounterClockwise { get; }

    /// <summary>Die Explosion, zu der sie zerfällt — sie bestimmt, was der Krater hinterlässt.</summary>
    public abstract ExplosionObject CreateExplosion();

    /// <summary>Die Richtung, die sie zuerst versucht.</summary>
    public CreatureFacing PreferredFacing => PrefersCounterClockwise
        ? Facing.TurnCounterClockwise()
        : Facing.TurnClockwise();

    /// <summary>Die Richtung, in die sie sich dreht, wenn Vorzug UND Geradeaus versperrt sind.</summary>
    public CreatureFacing BlockedFacing => PrefersCounterClockwise
        ? Facing.TurnClockwise()
        : Facing.TurnCounterClockwise();

    public override byte ToRaw() => (byte)(base.ToRaw() | (byte)Facing);

    public override void Interact()
    {
        if (ScannedThisFrame)
        {
            return;
        }

        var width = Cave.Width;

        // Rockford und Amoeba zünden ringsum; ein fallender Stein/Diamant zündet von oben.
        // Ein Rockford bzw. eine Amoeba, die sich in DIESEM Scan schon bewegt hat, zündet ebenfalls
        // — die Spezifikation zählt "Rockford, scanned this frame" ausdrücklich zu den Auslösern.
        // Das DOS-Original prüfte hier beim Schmetterling (anders als beim Geist) mit 0xFE und
        // übersah diesen Fall.
        //
        // "Fallendes Objekt darüber" prüfte das Original beim Schmetterling mit 0x4C==0x40 (:765) —
        // Fall-Bit gesetzt, Elementbits 2 und 3 frei, also ausschließlich Stein und Diamant. Beim
        // Geist stand dort 0x42 (:807), was zusätzlich die DIAMANT-EXPLOSION (Element 14) trifft:
        // Eine Schmetterlings-Explosion hätte eine Kreatur zwei Reihen tiefer gezündet, als wäre ein
        // Stein auf sie gefallen. Es gilt die saubere Prüfung, die auch der Stein-tötet-Rockford-Test
        // benutzt (:884).
        if (Neighbour(-width).TriggersCreature ||
            Neighbour(width).TriggersCreature ||
            Neighbour(-1).TriggersCreature ||
            Neighbour(1).TriggersCreature ||
            Neighbour(-width) is FallingObject { Falling: true })
        {
            Explode(Index, CreateExplosion);
            return;
        }

        if (Neighbour(PreferredFacing.Offset(width)).IsFreeSpace)
        {
            MoveTo(PreferredFacing);
        }
        else if (Neighbour(Facing.Offset(width)).IsFreeSpace)
        {
            MoveTo(Facing);
        }
        else
        {
            // Beides versperrt: einmal zur Gegenseite drehen, aber stehen bleiben.
            Facing = BlockedFacing;
            ScannedThisFrame = true;
        }
    }

    /// <summary>Zieht ins Nachbarfeld und räumt das alte.</summary>
    private void MoveTo(CreatureFacing facing)
    {
        var from = Index;

        Facing = facing;
        ScannedThisFrame = true;

        Cave.Set(from + facing.Offset(Cave.Width), this);
        Cave.Spawn(from, new EmptyObject(Cave) { ScannedThisFrame = true });
    }
}
