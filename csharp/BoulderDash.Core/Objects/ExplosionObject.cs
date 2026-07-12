using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Die Explosion — eine kurze Animation, die eine Kachel für sieben Phasen belegt und dann
/// vergeht (explosion(), BOULDER.CPP:709-721). Was sie hinterlässt, entscheidet die Unterklasse:
/// hier Leerraum, beim Schmetterling ein Diamant (JewelExplosionObject).
///
/// <b>Der Zähler ist absichtlich gemeinsam getaktet.</b> Im Original war wechsel_explo EINE globale
/// Variable, die jede NEUE Explosion auf 1 zurücksetzte — auch für Explosionen, die anderswo in der
/// Cave schon halb abgelaufen waren; alle verschwanden dann gemeinsam. Jede Explosion führt ihren
/// Zähler jetzt selbst, aber CavePhysics.Explode setzt ihn weiterhin auf allen zurück. Aus einem
/// unsichtbaren Nebeneffekt einer geteilten Variable wird damit eine benannte Regel; das Verhalten
/// bleibt gleich. Sollen Explosionen künftig unabhängig ablaufen, ist das eine bewusste Änderung.
/// </summary>
public class ExplosionObject : CaveObject
{
    /// <summary>Phase, in der die Explosion vergeht und das Objekt darunter freigibt.</summary>
    public const byte FinalPhase = 7;

    public override Element Element => Element.Explosion;

    public override int DefaultFrame => 52;

    /// <summary>Eigener Animationszähler (wechsel_explo), 1..7 — nicht der gemeinsame Achtertakt.
    /// 0 heißt: noch nicht angelaufen (so entsteht die Explosion des Eingangsaufbaus).</summary>
    public byte ExplosionPhase { get; set; }

    /// <summary>
    /// Bit 0x40 des Original-Explosionsbytes. Die Kreaturen sprengen mit 0xCC/0xCE (Bit gesetzt),
    /// der Stein, der Rockford erschlägt, mit 0x8C (Bit frei) — BOULDER.CPP:765, 807, 884. Für das
    /// Verhalten ist das Bit vollkommen bedeutungslos: Keine einzige Maskenprüfung liest es bei den
    /// Element-IDs 12/14 je aus. Es wird nur mitgeführt, damit <see cref="ToRaw"/> das Originalbyte
    /// exakt reproduziert und der Golden-Hash bit-genau vergleichbar bleibt.
    /// </summary>
    public bool CausedByCreature { get; init; }

    /// <summary>Was die Explosion hinterlässt, wenn sie vergeht.</summary>
    public virtual CaveObject Remnant() => new EmptyObject { Scanned = true };

    public override TileAppearance Appearance(in RenderContext ctx) =>
        TileAppearance.Of(DefaultFrame + ExplosionPhase);

    /// <summary>Schaltet den Explosionszähler weiter (statt des gemeinsamen Achtertakts): erst wenn
    /// die Explosion angelaufen ist, und dann bis zur Endphase, auf der er stehen bleibt
    /// (GameTick, BOULDER.CPP:229-233).</summary>
    public override void Animate(InputState input)
    {
        if (ExplosionPhase == 0)
        {
            return;
        }

        ExplosionPhase = Math.Min((byte)(ExplosionPhase + 1), FinalPhase);
    }

    public override byte ToRaw() => (byte)(base.ToRaw() | (CausedByCreature ? 0x40 : 0));
}
