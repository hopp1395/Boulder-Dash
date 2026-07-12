using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Was fallen kann: Stein und Diamant. Sie teilen sich das komplette Fallverhalten (regel(),
/// BOULDER.CPP:842-887) — sie fallen in Leerraum, rollen von abgerundeten Nachbarn ab, erschlagen
/// Rockford, wenn sie fallend auf ihm landen, und werden von der Zaubermauer in ihr Gegenstück
/// umgewandelt. Sie unterscheiden sich nur darin, WAS dabei herauskommt und wie es klingt.
/// </summary>
public abstract class FallingObject : CaveObject
{
    protected FallingObject(Cave cave)
        : base(cave)
    {
    }

    /// <summary>Bit 0x40: Das Objekt fällt gerade wirklich (statt nur zu liegen). Nur ein fallendes
    /// erschlägt Rockford, löst den Landeklang aus und wird von der Zaubermauer umgewandelt.</summary>
    public bool Falling { get; set; }

    /// <summary>
    /// Ein anderer Stein/Diamant fällt auf MICH. Liege ich ruhig, bin ich rund und er rollt an mir ab
    /// (BDCFF 0000: "rounded" sind die Mauer und RUHENDE Steine/Diamanten). Falle ich selbst gerade,
    /// bin ich kein Halt zum Abrollen — dann landet er auf mir.
    ///
    /// Das DOS-Original prüfte hier nur die Element-ID und ließ deshalb auch von fallenden Objekten
    /// abrollen.
    /// </summary>
    public override void ReceiveFalling(FallingObject faller)
    {
        if (Falling)
        {
            faller.Land();
        }
        else
        {
            faller.RollOff();
        }
    }

    /// <summary>Der Klang, wenn dieses Objekt aufschlägt.</summary>
    public abstract SoundEvent LandingSound { get; }

    /// <summary>Was die Zaubermauer daraus macht: Stein wird Diamant, Diamant wird Stein. Das
    /// Ergebnis fällt zwei Zeilen unter der Mauer weiter, ist also schon in Bewegung.</summary>
    public abstract FallingObject EnchantedWallProduct();

    public override byte ToRaw() => (byte)(base.ToRaw() | (Falling ? 0x40 : 0));

    /// <summary>Der Zug eines Steins/Diamanten ist denkbar kurz: Er wendet sich an den Boden unter
    /// sich, und der sagt ihm, was passiert (siehe CaveObject.ReceiveFalling).</summary>
    public override void Interact()
    {
        if (ScannedThisFrame)
        {
            return;
        }

        Neighbour(Cave.Width).ReceiveFalling(this);
    }

    /// <summary>Fällt eine Kachel tiefer — der Boden darunter ist Leerraum.</summary>
    public void FallThrough()
    {
        Falling = true;
        MoveTo(Index + Cave.Width);
    }

    /// <summary>Erschlägt Rockford, sofern das Objekt wirklich fällt — ein ruhendes liegt einfach auf
    /// ihm (das Original prüft dafür "(*ptr &amp; 0x4C) == 0x40", BOULDER.CPP:884).</summary>
    public void Crush(RockfordObject victim)
    {
        if (!Falling)
        {
            return;
        }

        Explode(victim.Index, () => new ExplosionObject(Cave));
    }

    /// <summary>Rollt zur Seite ab, wenn dort UND darunter unberührter Leerraum ist — sonst kommt das
    /// Objekt zur Ruhe. Links hat Vorrang vor rechts.</summary>
    public void RollOff()
    {
        foreach (var side in (ReadOnlySpan<int>)[-1, 1])
        {
            var target = Index + side;
            var belowTarget = target + Cave.Width;

            if (!Cave.Get(target).IsFreeSpace || !Cave.Get(belowTarget).IsFreeSpace)
            {
                continue;
            }

            // Das Fall-Momentum bleibt dabei, wie es ist: Ein ruhender Stein rollt ab, ohne schon zu
            // fallen — erst im Folge-Scan hat er Leerraum unter sich und fängt an.
            MoveTo(target);

            // Die Zelle unter dem Ziel wird gesperrt, damit in diesem Scan nichts hineinfällt.
            Cave.Get(belowTarget).ScannedThisFrame = true;
            return;
        }

        Land();
    }

    /// <summary>
    /// Auftreffen auf der Zaubermauer (BDCFF 0002): Läuft sie noch, kommt das Objekt zwei Zeilen
    /// tiefer als sein Gegenstück wieder heraus — ist dort kein Platz, ist es verloren. Ist ihre Zeit
    /// abgelaufen (oder war sie nie aktiv), verschluckt sie es ersatzlos.
    ///
    /// Gemeldet wird stets der Klang des Objekts, das unten HERAUSKOMMT — ein Stein klingt hier also
    /// nach Diamant und umgekehrt ("When falling boulders hit magic walls, a diamond sound plays
    /// regardless of outcome", BDCFF 0000). Das gilt in allen drei Zuständen der Mauer, auch wenn sie
    /// das Objekt verschluckt. Das DOS-Original schwieg hier ganz.
    /// </summary>
    public void EnterEnchantedWall()
    {
        // Ein ruhendes Objekt liegt einfach auf der Mauer.
        if (!Falling)
        {
            return;
        }

        var state = Cave.State;
        var product = EnchantedWallProduct();
        state.SoundEvents.Enqueue(product.LandingSound);

        if (state.EnchantedWallTimeRemaining == 0)
        {
            // Abgelaufene (oder nie aktivierte) Mauer: das Objekt verschwindet ersatzlos. Die
            // geräumte Zelle bleibt dabei UNverarbeitet — anders als in allen übrigen Zweigen.
            Cave.Spawn(Index, new EmptyObject(Cave));
            return;
        }

        state.EnchantedWallRunning = true;

        var exitIndex = Index + (2 * Cave.Width);
        if (Cave.Get(exitIndex).IsFreeSpace)
        {
            product.ScannedThisFrame = true;
            Cave.Spawn(exitIndex, product);
        }

        // Steht unter der Mauer etwas im Weg, wird das Objekt trotzdem gelöscht:
        // "the boulder or diamond is lost" (BDCFF 0002).
        Cave.Spawn(Index, new EmptyObject(Cave) { ScannedThisFrame = true });
    }

    /// <summary>Zieht auf die Zielkachel um und lässt verarbeiteten Leerraum zurück.</summary>
    private void MoveTo(int target)
    {
        var from = Index;

        ScannedThisFrame = true;
        Cave.Set(target, this);
        Cave.Spawn(from, new EmptyObject(Cave) { ScannedThisFrame = true });
    }

    /// <summary>Kommt zur Ruhe. Der Aufschlag klingt nur, wenn das Objekt wirklich fiel — ein bereits
    /// ruhendes löst kein Sound-Ereignis aus.</summary>
    public void Land()
    {
        if (!Falling)
        {
            return;
        }

        Cave.State.SoundEvents.Enqueue(LandingSound);
        Falling = false;
    }
}
