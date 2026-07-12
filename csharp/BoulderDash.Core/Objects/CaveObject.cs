using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Ein Objekt im Cave — Stein, Diamant, Erde, Geist, Rockford, die Wände, die Explosionen. Genau
/// eine Instanz je belegter Kachel; das Cave-Gitter besteht aus ihnen.
///
/// Die Basisklasse trägt, was alle Objekte gemeinsam haben: ihre Identität (<see cref="Element"/>,
/// die persistente ID des Dateiformats), ihren Zustand im laufenden Cave-Scan
/// (<see cref="Scanned"/>), ihren Animationstakt (<see cref="AnimationPhase"/>), ihr Aussehen
/// (<see cref="Appearance"/>) und die Prädikate, an denen die Physik ihr Verhalten unterscheidet.
/// Die konkreten Objekte erben das und ergänzen ihr Spezifisches — der Stein sein Fall-Momentum, die
/// Kreatur ihre Blickrichtung, Rockford seine Ruheanimation.
///
/// <b>Zustand statt Bitfelder:</b> Im Original (und bis vor Kurzem auch hier) steckte all das in den
/// Bits EINES Kachelbytes — 0x0F die Element-ID, 0x80 "verarbeitet", 0x40 Fall-Momentum, 0x60
/// Blickrichtung. Weil sich Stein und Kreatur dasselbe Byte teilten, überlappten Momentum- und
/// Richtungsbits. Sobald jeder Typ sein eigenes Feld hat, ist das gegenstandslos. <see cref="ToRaw"/>
/// rechnet den Zustand in das Originalbyte zurück — das braucht der Golden-Hash, und es ist zugleich
/// der Nachweis, dass das Objektmodell bit-genau dem alten Bytemodell entspricht.
/// </summary>
public abstract class CaveObject
{
    /// <summary>Periode des gemeinsamen Animationstakts (wechsel_vier, sprites_wechsel(),
    /// BOULDER.CPP:593-607).</summary>
    public const int AnimationPeriod = 8;

    /// <summary>Die persistente Kachel-ID — was in der Cave-Datei steht und was
    /// <see cref="ToRaw"/> in die unteren 4 Bits schreibt.</summary>
    public abstract Element Element { get; }

    /// <summary>Bit 0x80: In DIESEM Cave-Scan bereits verarbeitet. Verhindert, dass ein Objekt, das
    /// sich gerade bewegt hat, im selben Scan noch einmal drankommt — und wirkt nebenbei als Sperre:
    /// eine verarbeitete Zelle ist für Rockford unpassierbar und für die Amoeba kein Wuchsgrund.
    /// CavePhysics löscht das Flag am Ende jedes Scans für alle Kacheln.</summary>
    public bool Scanned { get; set; }

    /// <summary>Eigener Animationstakt, Periode 8. Im Original war das der EINE globale Zähler
    /// wechsel_vier für alle Objekte; jetzt führt jedes Objekt seinen eigenen. Sie laufen dennoch
    /// synchron, weil alle im selben Takt geschaltet und frisch erzeugte Objekte mit der aktuellen
    /// Cave-Phase geboren werden (siehe Cave.AnimationPhase) — das Bild bleibt damit unverändert.
    /// Erst wenn Objekte künftig unabhängig voneinander animieren sollen, laufen sie auseinander.</summary>
    public byte AnimationPhase { get; set; }

    /// <summary>Zeichen in der ASCII-Karte einer Cave-Datei (Legende siehe CaveAsciiMap). '?' für die
    /// Objekte, die nur im Spiel entstehen und nie in einer Datei stehen (Explosionen, Rand-Füllstein).</summary>
    public virtual char MapGlyph => '?';

    /// <summary>Frame, den ein frisch geladenes, noch nicht angelaufenes Spiel zeigt — die
    /// buffer[MASK_*]-Initialwerte aus Init_Pointer (INIT.CPP:187-202).</summary>
    public abstract int DefaultFrame { get; }

    /// <summary>Welches Bild diese Kachel gerade zeigt. Standardmäßig das unbewegte Startbild; alles
    /// Animierte überschreibt das (Frameauswahl wie sprites_wechsel(), BOULDER.CPP:593-646).</summary>
    public virtual TileAppearance Appearance(in RenderContext ctx) => TileAppearance.Of(DefaultFrame);

    /// <summary>Einen Animationsschritt weiterschalten — einmal pro Tick, für jede Kachel.</summary>
    public virtual void Animate(InputState input) =>
        AnimationPhase = (byte)((AnimationPhase + 1) % AnimationPeriod);

    /// <summary>Unzerstörbar: Die Explosion lässt es stehen. Das sind Stahlwand, Ein- und Ausgang —
    /// in BD1 sind Ein- und Ausgang Stahlwand-Varianten, der Ausgang sieht bis zu seiner
    /// Freischaltung ja auch aus wie Stahl. Die Zaubermauer gehört bewusst NICHT dazu, sie ist
    /// sprengbar; das DOS-Original verschonte nur die Stahlwand (explosion(), BOULDER.CPP:709-721).</summary>
    public virtual bool IsExplosionProof => false;

    /// <summary>Abgerundet: Ein fallendes Objekt rollt darüber zur Seite ab, statt liegen zu bleiben.
    /// Nach BDCFF 0000 sind das die Mauer und RUHENDE Steine/Diamanten (siehe FallingObject).</summary>
    public virtual bool IsRounded => false;

    /// <summary>Freier Leerraum: Hier darf etwas hineinfallen, -rollen oder -ziehen. Eine in diesem
    /// Scan schon verarbeitete Leerzelle zählt NICHT — im Original war das die Prüfung auf das
    /// nackte Byte 0 (statt nur auf die Element-ID).</summary>
    public virtual bool IsFreeSpace => false;

    /// <summary>Ob die Amoeba in diese Kachel hineinwachsen kann: Leerraum oder Erde, und in diesem
    /// Scan noch unberührt (Original: die wiederkehrende Prüfung "(x &amp; 0xFE) == 0").</summary>
    public virtual bool CanAmoebaGrowInto => false;

    /// <summary>Zündet eine benachbarte Kreatur. Das sind Rockford und die Amoeba — im Original die
    /// Maske "(x &amp; 0x7E) == 6", die Bit 0 offen lässt und damit die Element-IDs 6 UND 7 trifft.</summary>
    public virtual bool TriggersCreature => false;

    /// <summary>Das Kachelbyte des Originals: Element-ID in den unteren 4 Bits, darüber die Flags.
    /// Dient nur noch der Serialisierung (Golden-Hash) — die Physik liest keine Bytes mehr.</summary>
    public virtual byte ToRaw() => (byte)((byte)Element | (Scanned ? 0x80 : 0));
}
