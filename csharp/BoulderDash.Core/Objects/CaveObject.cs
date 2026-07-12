using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Ein Objekt im Cave — Stein, Diamant, Erde, Geist, Rockford, die Wände, die Explosionen. Genau
/// eine Instanz je Kachel; das Cave-Gitter besteht aus ihnen.
///
/// <b>Jedes Objekt rechnet sich selbst aus.</b> Es kennt seine <see cref="Cave"/> und seinen
/// <see cref="Index"/> darin, kann also seine Nachbarn ansehen, sich bewegen, etwas einsammeln oder
/// sprengen. Die Cave gibt nur den Takt vor: <see cref="NextFrame"/> je Tick (Animation),
/// <see cref="Interact"/> je Cave-Scan (Physik). Die Spielregeln eines Objekts stehen damit beim
/// Objekt — der Stein weiß, wie er fällt, die Kreatur, wie sie an der Wand entlangläuft.
///
/// Die Basisklasse trägt, was alle gemeinsam haben: Identität (<see cref="Element"/>, die persistente
/// ID des Dateiformats), Platz und Scan-Zustand, Animationsphase, Aussehen und die Prädikate, an
/// denen die anderen Objekte ihr Verhalten unterscheiden. Wer nichts tut (Erde, Wände), erbt einfach
/// das leere <see cref="Interact"/>.
///
/// <b>Zustand statt Bitfelder:</b> Im Original steckte all das in den Bits EINES Kachelbytes — 0x0F
/// die Element-ID, 0x80 "verarbeitet", 0x40 Fall-Momentum, 0x60 Blickrichtung. Weil sich Stein und
/// Kreatur dasselbe Byte teilten, überlappten Momentum- und Richtungsbits. Sobald jeder Typ sein
/// eigenes Feld hat, ist das gegenstandslos. <see cref="ToRaw"/> rechnet den Zustand ins
/// Originalbyte zurück — das braucht der Golden-Hash, und es ist zugleich der Nachweis, dass das
/// Objektmodell bit-genau dem alten Bytemodell entspricht.
/// </summary>
public abstract class CaveObject
{
    /// <summary>Periode des gemeinsamen Animationstakts (wechsel_vier, sprites_wechsel(),
    /// BOULDER.CPP:593-607).</summary>
    public const int AnimationPeriod = 8;

    /// <param name="cave">Die Höhle, in der dieses Objekt lebt — Pflicht. Ein Objekt ohne Welt könnte
    /// weder seine Nachbarn ansehen noch sich bewegen. Was zu keinem Spielgitter gehört (Prototypen,
    /// der Rand-Füllstein der Verdeckung), bekommt <see cref="Simulation.Cave.Nowhere"/>.</param>
    protected CaveObject(Cave cave)
    {
        Cave = cave;
    }

    /// <summary>Die Höhle, in der dieses Objekt lebt — sein Zugang zu den Nachbarkacheln und zu dem,
    /// was der ganzen Cave gehört (Punkte, Sound-Ereignisse, Rockfords Steuerung, die Kamera).</summary>
    protected Cave Cave { get; }

    /// <summary>Wo dieses Objekt im Gitter steht. Die Cave pflegt den Wert bei jedem Setzen; ein
    /// Objekt, das sich bewegt, behält seine Instanz und bekommt nur einen neuen Index.</summary>
    public int Index { get; internal set; }

    /// <summary>Die persistente Kachel-ID — was in der Cave-Datei steht und was
    /// <see cref="ToRaw"/> in die unteren 4 Bits schreibt.</summary>
    public abstract Element Element { get; }

    /// <summary>Bit 0x80: In DIESEM Cave-Scan bereits verarbeitet. Verhindert, dass ein Objekt, das
    /// sich gerade bewegt hat, im selben Scan noch einmal drankommt — und wirkt nebenbei als Sperre:
    /// eine verarbeitete Zelle ist für Rockford unpassierbar und für die Amoeba kein Wuchsgrund.
    /// Die Cave löscht das Flag am Ende jedes Scans für alle Kacheln.</summary>
    public bool ScannedThisFrame { get; set; }

    /// <summary>Eigener Animationstakt, Periode 8. Im Original war das der EINE globale Zähler
    /// wechsel_vier für alle Objekte; jetzt führt jedes Objekt seinen eigenen. Sie laufen dennoch
    /// synchron, weil alle im selben Takt geschaltet und frisch erzeugte Objekte mit der aktuellen
    /// Cave-Phase geboren werden — das Bild bleibt damit unverändert.</summary>
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

    /// <summary>
    /// Ob dieses Objekt im Nebel des Cave-Explore-Features noch zu sehen ist (siehe ExploreMap).
    /// Der Nebel zeigt, woran Rockford sich ERINNERT — und erinnern kann er sich nur an die Umgebung:
    /// Erde, Wände, Steine, Diamanten, die Amoeba. Was aus eigenem Antrieb umherzieht, weiß er nach
    /// dem Wegsehen nicht mehr; die Kreaturen (CreatureObject) sind deshalb außerhalb seines
    /// Blickradius unsichtbar und geben die Kachel frei.
    ///
    /// Keine Original-Entsprechung — das Feature gibt es weder im DOS-Original noch in BD1.
    /// </summary>
    public virtual bool VisibleInFog => true;

    /// <summary>
    /// Ob die Bildschirm-Verdeckung (ScreenCover) diese Kachel überzeichnen darf. Sie tut es mit dem
    /// Rand-Füllstein, einer laufenden Stahlwand — und die gehört über die Höhle, nicht über das
    /// Nichts um sie herum: Sonst deckte sich beim Cave-Start und -Ende ein Stahl-RECHTECK auf und zu
    /// statt der Silhouette der Höhle. Einzig <see cref="VoidObject"/> sagt hier nein.
    /// </summary>
    public virtual bool CoveredByScreen => true;

    /// <summary>Ein Animationsschritt — einmal pro Tick, für jede Kachel.</summary>
    public virtual void NextFrame() =>
        AnimationPhase = (byte)((AnimationPhase + 1) % AnimationPeriod);

    /// <summary>
    /// Der Zug dieses Objekts — einmal pro Cave-Scan. Hier stehen seine Spielregeln: Es sieht sich
    /// über die <see cref="Cave"/> seine Nachbarn an und verändert sie und sich selbst. Wer keine
    /// Regeln hat (Erde, Wände, Türen), tut nichts.
    ///
    /// Jede Regel beginnt mit der Prüfung auf <see cref="ScannedThisFrame"/>: Ein Objekt, das sich in
    /// diesem Scan schon bewegt hat, ist fertig — sonst liefe ein fallender Stein seiner eigenen
    /// Bewegung hinterher, weil der Scan von oben nach unten läuft.
    /// </summary>
    public virtual void Interact()
    {
    }

    /// <summary>Schließt den Cave-Scan für dieses Objekt ab: Das Verarbeitet-Flag fällt, damit es im
    /// nächsten Scan wieder ziehen darf (regel(), BOULDER.CPP:930-934).</summary>
    public virtual void EndScan() => ScannedThisFrame = false;

    /// <summary>
    /// Eine Explosion erfasst diese Kachel. Das Objekt entscheidet SELBST, was das für es bedeutet:
    /// Der Normalfall ist, dass es dabei draufgeht und der Explosion Platz macht. Wer das übersteht,
    /// überschreibt diese Methode und tut schlicht nichts — Stahlwand, Ein- und Ausgang
    /// (explosion(), BOULDER.CPP:709-721).
    ///
    /// Das neue Explosionsobjekt wird erst hier erzeugt, damit jede Kachel ihre eigene Instanz
    /// bekommt — und damit die Unzerstörbaren gar keine erst anfordern.
    /// </summary>
    public virtual void Detonate(Func<ExplosionObject> create)
    {
        var explosion = create();
        explosion.ScannedThisFrame = true;
        Cave.Spawn(Index, explosion);
    }

    /// <summary>
    /// Ein Stein oder Diamant fällt auf diese Kachel. Was dabei passiert, entscheidet der BODEN, nicht
    /// der Fallende — er kennt sich selbst am besten: Auf Erde und Stahl kommt das Objekt zur Ruhe
    /// (der Normalfall hier), durch Leerraum fällt es hindurch, von einer Mauer rollt es ab, in der
    /// Zaubermauer wandelt es sich, und auf Rockford geht es tödlich aus.
    ///
    /// Das ist doppelte Dispatch: Der Boden sagt dem Fallenden, was er zu tun hat — WIE er fällt,
    /// abrollt oder landet, weiß nur er selbst (siehe FallingObject).
    /// </summary>
    public virtual void ReceiveFalling(FallingObject faller) => faller.Land();

    /// <summary>Freier Leerraum: Hier darf etwas hineinfallen, -rollen oder -ziehen. Eine in diesem
    /// Scan schon verarbeitete Leerzelle zählt NICHT — im Original war das die Prüfung auf das
    /// nackte Byte 0 (statt nur auf die Element-ID).</summary>
    public virtual bool IsFreeSpace => false;

    /// <summary>Zündet eine benachbarte Kreatur. Das sind Rockford und die Amoeba — im Original die
    /// Maske "(x &amp; 0x7E) == 6", die Bit 0 offen lässt und damit die Element-IDs 6 UND 7 trifft.</summary>
    public virtual bool TriggersCreature => false;

    /// <summary>Das Kachelbyte des Originals: Element-ID in den unteren 4 Bits, darüber die Flags.
    /// Dient nur noch der Serialisierung (Golden-Hash) — die Physik liest keine Bytes mehr.</summary>
    public virtual byte ToRaw() => (byte)((byte)Element | (ScannedThisFrame ? 0x80 : 0));

    /// <summary>Der Nachbar in die angegebene Gitterrichtung (-Width = oben, +1 = rechts, ...).</summary>
    protected CaveObject Neighbour(int offset) => Cave.Get(Index + offset);

    /// <summary>
    /// explosion(): Sprengt den 3x3-Bereich um eine Kachel (:709-721). Jede getroffene Kachel bekommt
    /// gesagt, dass es sie erwischt hat, und entscheidet selbst, ob sie das überlebt
    /// (<see cref="Detonate"/>) — der Stahl bleibt stehen. Welche Art Explosion entsteht, entscheidet
    /// dagegen der Verursacher: Ein Schmetterling hinterlässt Diamanten, alle anderen Leerraum.
    ///
    /// Anschließend fangen ALLE Explosionen der Höhle wieder bei Phase 1 an, auch die anderswo schon
    /// halb abgelaufenen. Im Original war wechsel_explo eine einzige globale Variable, die jede neue
    /// Explosion auf 1 zurücksetzte — sie verschwanden dadurch stets gemeinsam. Die Kopplung ist
    /// bewusst erhalten (siehe ExplosionObject).
    /// </summary>
    protected void Explode(int centerIndex, Func<ExplosionObject> create)
    {
        var width = Cave.Width;
        ReadOnlySpan<int> offsets =
        [
            -width - 1, -width, -width + 1,
            -1, 0, 1,
            width - 1, width, width + 1,
        ];

        foreach (var offset in offsets)
        {
            Cave.Get(centerIndex + offset).Detonate(create);
        }

        Cave.RestartExplosions();
        Cave.State.SoundEvents.Enqueue(SoundEvent.Explosion);
    }
}
