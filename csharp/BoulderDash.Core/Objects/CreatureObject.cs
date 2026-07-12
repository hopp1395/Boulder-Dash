using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Geist und Schmetterling. Beide patrouillieren an einer Wand entlang, indem sie stur ihre
/// Vorzugsdrehung versuchen: Der Geist hält sich links (gegen den Uhrzeigersinn), der Schmetterling
/// rechts. Geht das nicht, ziehen sie geradeaus; geht auch das nicht, drehen sie sich genau EINMAL
/// zur Gegenseite und bleiben in diesem Scan stehen ("if a firefly is forced to turn against its
/// 'preferred direction', it does not actually move for that frame", BDCFF 0008/0009).
///
/// Berührt sie Rockford oder die Amoeba, oder fällt ihr ein Stein auf den Kopf, explodiert sie —
/// der Geist zu Leerraum, der Schmetterling zu Diamanten.
///
/// Das DOS-Original (BOULDER.CPP:758-840) drehte stattdessen in einer Schleife bis zu viermal weiter
/// und unterdrückte die Bewegung über ein Extra-Bit 0x10. Die Bewegung stimmte damit zwar, die
/// resultierende BLICKRICHTUNG einer blockierten Kreatur aber nicht — und der Schmetterling drehte
/// bei Blockade sogar auf seine Vorzugsseite statt auf die Gegenseite.
/// </summary>
public abstract class CreatureObject : CaveObject
{
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
}
