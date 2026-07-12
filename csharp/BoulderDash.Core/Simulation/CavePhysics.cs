namespace BoulderDash.Core.Simulation;

/// <summary>
/// Byte-genaue Transliteration von regel() (src/BOULDER.CPP:725-959), explosion() (:709-721),
/// anfang() (:667-677) und ende() (:681-687). Arbeitet bewusst direkt auf rohen Kachelbytes statt
/// auf Element/Flag-Typen, weil die Original-Masken quer durch Bitgruppen schneiden (siehe
/// Cave-Klassenkommentar). Jede Regel ist einzeln mit der Original-Zeilennummer kommentiert.
/// </summary>
public sealed class CavePhysics
{
    /// <summary>Ab dieser Zellzahl wird die ganze Amoeba zu Boulders. Feste BD1-Konstante (in BD1
    /// nicht pro Cave einstellbar); das DOS-Original wandelte schon ab 96 Zellen um (BOULDER.CPP:951).</summary>
    private const int AmoebaMaxSize = 200;

    private readonly Random _random;

    public CavePhysics(Random random)
    {
        _random = random;
    }

    /// <summary>Kachel ist Leer(0) oder Erde(1), ohne Flags — die wiederkehrende Prüfung "(x&amp;0xFE)==0".</summary>
    private static bool IsEmptyOrEarthRaw(byte raw) => (raw & 0xFE) == 0;

    public void Regel(Cave cave, GameState state, InputState input, Camera camera)
    {
        var width = cave.Width;
        var height = cave.Height;

        // Die Amoeba entscheidet ihr Schicksal aus den Zahlen des VORIGEN Scans — laut Spezifikation
        // wirken "zu groß" und "eingeschlossen" jeweils erst im Folge-Scan, und "zu groß" hat Vorrang.
        byte amoebaFate = 0;
        if (state.AmoebaCountLastScan >= AmoebaMaxSize)
        {
            amoebaFate = (byte)Element.Boulder;
        }
        else if (state.AmoebaSuffocatedLastScan)
        {
            amoebaFate = (byte)Element.Jewel;
        }

        var amoebaFound = 0;
        var amoebaCanGrow = false;

        var wasAlive = state.Stat == 0;

        if (state.EntranceProgress > 100 && state.Stat == 0)
        {
            state.Stat = 1;
        }

        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < width; col++)
            {
                var idx = (row * width) + col;

                ResolveExplosion(cave, state, idx);
                ProcessAmoeba(cave, state, idx, width, amoebaFate, ref amoebaFound, ref amoebaCanGrow);
                ProcessButterfly(cave, state, idx, width);
                ProcessFirefly(cave, state, idx, width);
                ProcessBoulderOrJewel(cave, state, idx, width);
                ProcessRockford(cave, state, input, camera, idx, width, height, col, row);
            }
        }

        if (wasAlive && state.Stat != 0)
        {
            state.SoundEvents.Enqueue(SoundEvent.Death);
        }

        // Verarbeitet-Flag für den nächsten Sweep löschen (:930-934)
        for (var i = 0; i < width * height; i++)
        {
            cave.SetRaw(i, (byte)(cave.GetRaw(i) & 0x7F));
        }

        state.AmoebaCountLastScan = amoebaFound;
        state.AmoebaSuffocatedLastScan = amoebaFound > 0 && !amoebaCanGrow;

        // Für die Amoeba-Drone (kein Original-Äquivalent, siehe AudioPlayer/SoundRecipes).
        state.AmoebaPresent = amoebaFound > 0;
    }

    /// <summary>Explosion -&gt; Jewel / Explosion -&gt; Leer, wenn die Animation ausgelaufen ist (:740-743).</summary>
    private static void ResolveExplosion(Cave cave, GameState state, int idx)
    {
        var raw = cave.GetRaw(idx);
        if ((raw & 0x1F) == 12 && state.WechselExplo == 7)
        {
            cave.SetRaw(idx, 0x80);
        }

        if ((raw & 0x1F) == 14 && state.WechselExplo == 7)
        {
            cave.SetRaw(idx, 0x83);
        }
    }

    /// <summary>Amoeba nach BD1 statt nach dem DOS-Original (BDCFF-Objektspezifikation 000A,
    /// elmerproductions.com/sp/peterb/BDCFF/objects/000A.html): Jede Zelle würfelt pro Scan einzeln, ob sie
    /// wächst — mit 4/128 (~3 %), nach Ablauf der Amoeba-Zeit mit 4/16 (25 %) —, und wächst dann in genau
    /// eine zufällig gezogene der vier Richtungen, sofern dort Leerraum oder Erde liegt. Wird sie zu groß
    /// oder erstickt sie, wandelt der Folge-Scan sie über <paramref name="fate"/> um.
    /// Das DOS-Original ließ dagegen pro Scan nur eine einzige, per rand()%96 gewählte Zelle wachsen, und
    /// die immer in fester Richtungspriorität (BOULDER.CPP:745-755).</summary>
    private void ProcessAmoeba(
        Cave cave, GameState state, int idx, int width, byte fate, ref int found, ref bool canGrow)
    {
        var raw = cave.GetRaw(idx);
        if ((raw & 0x9F) != 0x07)
        {
            return;
        }

        if (fate != 0)
        {
            // Verarbeitet-Bit setzen, damit ein so entstandener Boulder nicht schon im selben Scan
            // weiterfällt (dasselbe Muster wie ResolveExplosion).
            cave.SetRaw(idx, (byte)(0x80 | fate));
            return;
        }

        found++;

        ReadOnlySpan<int> directions = [-width, width, -1, 1];
        foreach (var direction in directions)
        {
            if (IsEmptyOrEarthRaw(cave.GetRaw(idx + direction)))
            {
                canGrow = true;
                break;
            }
        }

        if (_random.Next(state.AmoebaSlowGrowthRemaining > 0 ? 128 : 16) >= 4)
        {
            return;
        }

        var target = idx + directions[_random.Next(directions.Length)];
        if (IsEmptyOrEarthRaw(cave.GetRaw(target)))
        {
            // 0x87: frische Amoeba mit Verarbeitet-Bit — sie wächst erst im nächsten Scan weiter und
            // zählt auch erst dann mit ("newly grown amoeba cannot expand until the following frame").
            cave.SetRaw(target, 0x87);
        }
    }

    /// <summary>Schmetterling/Butterfly: dreht sich bevorzugt im Uhrzeigersinn und explodiert zu Jewels.</summary>
    private static void ProcessButterfly(Cave cave, GameState state, int idx, int width) =>
        ProcessCreature(cave, state, idx, width, element: 9, preferCcw: false, explosionAnim: 0xCE);

    /// <summary>Geist/Firefly: dreht sich bevorzugt gegen den Uhrzeigersinn und explodiert zu Leere.</summary>
    private static void ProcessFirefly(Cave cave, GameState state, int idx, int width) =>
        ProcessCreature(cave, state, idx, width, element: 8, preferCcw: true, explosionAnim: 0xCC);

    /// <summary>
    /// Firefly und Butterfly nach BD1 (BDCFF-Objektspezifikationen 0008/0009,
    /// elmerproductions.com/sp/peterb/BDCFF/objects/0008.html): Die Kreatur versucht zuerst ihre
    /// Vorzugsdrehung (Firefly nach links = gegen den Uhrzeigersinn, Butterfly nach rechts = im
    /// Uhrzeigersinn), sonst geradeaus. Ist beides versperrt, dreht sie sich genau EINMAL zur
    /// Gegenseite und zieht in diesem Scan nicht ("if a firefly is forced to turn against its
    /// 'preferred direction', it does not actually move for that frame").
    ///
    /// Das DOS-Original (BOULDER.CPP:758-840) drehte stattdessen in einer Schleife bis zu viermal
    /// weiter und unterdrückte die Bewegung über ein Extra-Bit 0x10. Die Bewegung stimmte damit zwar,
    /// die resultierende BLICKRICHTUNG einer blockierten Kreatur aber nicht — und der Butterfly drehte
    /// bei Blockade sogar auf seine Vorzugsseite statt auf die Gegenseite. Beides ist hier korrigiert;
    /// das Bit 0x10 entfällt ersatzlos.
    /// </summary>
    private static void ProcessCreature(
        Cave cave, GameState state, int idx, int width, byte element, bool preferCcw, byte explosionAnim)
    {
        var raw = cave.GetRaw(idx);
        if ((raw & 0x8F) != element)
        {
            return;
        }

        // Kontakt zu Rockford (6) oder Amoeba (7) ringsum, oder ein fallendes Objekt direkt darüber.
        // Die Maske 0x7E lässt Bit 0x80 bewusst offen: ein Rockford bzw. eine Amoeba, die sich in
        // DIESEM Scan schon bewegt hat, trägt das Verarbeitet-Bit und zündet trotzdem — die BDCFF-
        // Spezifikation zählt "Rockford, scanned this frame" ausdrücklich zu den Auslösern. Das
        // DOS-Original prüfte hier beim Butterfly (anders als beim Firefly) mit 0xFE und übersah
        // diesen Fall.
        if ((cave.GetRaw(idx - width) & 0x7E) == 6 ||
            (cave.GetRaw(idx + width) & 0x7E) == 6 ||
            (cave.GetRaw(idx - 1) & 0x7E) == 6 ||
            (cave.GetRaw(idx + 1) & 0x7E) == 6 ||
            (cave.GetRaw(idx - width) & 0x42) == 0x42)
        {
            Explode(cave, state, idx, explosionAnim);
            return;
        }

        var direction = raw & 0x60;
        var preferred = preferCcw ? TurnCcw(direction) : TurnCw(direction);

        if (cave.GetRaw(idx + DirectionOffset(preferred, width)) == 0)
        {
            MoveCreature(cave, idx, width, element, preferred);
        }
        else if (cave.GetRaw(idx + DirectionOffset(direction, width)) == 0)
        {
            MoveCreature(cave, idx, width, element, direction);
        }
        else
        {
            // Beides versperrt: einmal zur Gegenseite drehen, aber stehen bleiben.
            var turned = preferCcw ? TurnCw(direction) : TurnCcw(direction);
            cave.SetRaw(idx, (byte)(0x80 | turned | element));
        }
    }

    /// <summary>Setzt die Kreatur mit Verarbeitet-Bit ins Nachbarfeld und räumt ihr altes.</summary>
    private static void MoveCreature(Cave cave, int idx, int width, byte element, int direction)
    {
        cave.SetRaw(idx + DirectionOffset(direction, width), (byte)(0x80 | direction | element));
        cave.SetRaw(idx, 0x80);
    }

    /// <summary>Index-Offset der Kreaturen-Richtungsbits: 0x00 links, 0x20 oben, 0x40 rechts, 0x60 unten
    /// (dieselbe Reihenfolge wie die BDCFF-Attributbits 00/01/10/11).</summary>
    private static int DirectionOffset(int direction, int width) => direction switch
    {
        0x00 => -1,
        0x20 => -width,
        0x40 => 1,
        _ => width,
    };

    private static int TurnCw(int direction) => (direction + 0x20) & 0x60;

    private static int TurnCcw(int direction) => (direction - 0x20) & 0x60;

    /// <summary>Boulder/Jewel: fällt, rollt ab, wandelt sich am EnchantedWall, tötet Rockford beim Landen (:842-887).</summary>
    private static void ProcessBoulderOrJewel(Cave cave, GameState state, int idx, int width)
    {
        var raw = cave.GetRaw(idx);
        if ((raw & 0x9F) != 3 && (raw & 0x9F) != 2)
        {
            return;
        }

        var below = cave.GetRaw(idx + width) & 0x0F;
        switch (below)
        {
            case 0:
                cave.SetRaw(idx + width, (byte)(cave.GetRaw(idx) | 0xC0));
                cave.SetRaw(idx, 0x80);
                break;
            case 2:
            case 3:
            case 4:
                if (cave.GetRaw(idx - 1) == 0 && cave.GetRaw((idx - 1) + width) == 0)
                {
                    cave.SetRaw(idx - 1, (byte)(cave.GetRaw(idx) | 0x80));
                    cave.SetRaw(idx, 0x80);
                    cave.SetRaw((idx - 1) + width, 0x80);
                }
                else if (cave.GetRaw(idx + 1) == 0 && cave.GetRaw((idx + 1) + width) == 0)
                {
                    cave.SetRaw(idx + 1, (byte)(cave.GetRaw(idx) | 0x80));
                    cave.SetRaw(idx, 0x80);
                    cave.SetRaw((idx + 1) + width, 0x80);
                }
                else
                {
                    EnqueueLandingSoundIfFalling(state, cave.GetRaw(idx), raw);
                    cave.SetRaw(idx, (byte)(cave.GetRaw(idx) & 0x1F));
                }

                break;
            case 13:
                // Nur ein FALLENDES Objekt trifft auf die Mauer — ein ruhendes liegt einfach darauf.
                if ((cave.GetRaw(idx) & 0x40) != 0x40)
                {
                    break;
                }

                EnqueueEnchantedWallSound(state, raw);

                if (state.EnchantedWallTimeRemaining > 0)
                {
                    state.EnchantedWallRunning = true;
                    if (cave.GetRaw(idx + (2 * width)) == 0)
                    {
                        var minusOne = (byte)(cave.GetRaw(idx) - 1);
                        cave.SetRaw(idx + (2 * width), (byte)(minusOne | 0xC2));
                    }

                    // Steht unter der Mauer etwas im Weg, wird das Objekt trotzdem gelöscht:
                    // "the boulder or diamond is lost" (BDCFF 0002).
                    cave.SetRaw(idx, 0x80);
                }
                else
                {
                    // Abgelaufene (oder nie aktivierte) Mauer: das Objekt verschwindet ersatzlos.
                    cave.SetRaw(idx, 0);
                }

                break;
            case 6:
                if ((cave.GetRaw(idx + width) & 0x06) == 0x06 && (cave.GetRaw(idx) & 0x4C) == 0x40)
                {
                    Explode(cave, state, idx + width, 0x8C);
                }

                break;
            case 8:
            case 9:
                break;
            default:
                EnqueueLandingSoundIfFalling(state, cave.GetRaw(idx), raw);
                cave.SetRaw(idx, (byte)(cave.GetRaw(idx) & 0x1F));
                break;
        }
    }

    /// <summary>Auftreffen auf der Zaubermauer. Gemeldet wird der Klang des Objekts, das unten wieder
    /// HERAUSKOMMT — ein Boulder klingt hier also nach Jewel und umgekehrt (BDCFF 0000: "When falling
    /// boulders hit magic walls, a diamond sound plays regardless of outcome"). Das gilt in allen drei
    /// Zuständen der Mauer, auch wenn sie das Objekt verschluckt. Das DOS-Original schwieg hier ganz.</summary>
    private static void EnqueueEnchantedWallSound(GameState state, byte raw) =>
        state.SoundEvents.Enqueue((raw & 0x0F) == 3 ? SoundEvent.BoulderLand : SoundEvent.JewelLand);

    /// <summary>Meldet BoulderLand/JewelLand nur, wenn das Objekt gerade wirklich aktiv fiel
    /// (Momentum-Bit 0x40 gesetzt) — ein bereits ruhendes Objekt löst kein Sound-Ereignis aus.</summary>
    private static void EnqueueLandingSoundIfFalling(GameState state, byte currentRaw, byte originalRaw)
    {
        if ((currentRaw & 0x40) != 0x40)
        {
            return;
        }

        state.SoundEvents.Enqueue((originalRaw & 0x0F) == 3 ? SoundEvent.JewelLand : SoundEvent.BoulderLand);
    }

    /// <summary>Rockford: Kamera-Scroll-Auslöser plus Bewegung/Graben/Sammeln/Schieben (:890-923).
    /// Original-Eigenheit (Dangling-Else): die "else"-Bewegungsverarbeitung bindet nur an die
    /// vierte Kamerabedingung — löst diese den Aufwärtsscroll aus, bleibt die Bewegung diesen
    /// Tick komplett aus, auch wenn keine der anderen drei Kamerabedingungen zutraf.</summary>
    private void ProcessRockford(
        Cave cave, GameState state, InputState input, Camera camera,
        int idx, int width, int height, int col, int row)
    {
        var raw = cave.GetRaw(idx);
        if ((raw & 0x9F) != 6)
        {
            return;
        }

        state.Stat = 0;

        if (camera.X + 17 < col && camera.X < width - 20)
        {
            camera.Relx = 7;
        }

        if (camera.X + 1 == col && camera.X > 0)
        {
            camera.Relx = -7;
        }

        if (camera.Y + 9 < row && camera.Y < height - 12)
        {
            camera.Rely = 5;
        }

        if (camera.Y + 1 == row && camera.Y > 0)
        {
            camera.Rely = -5;
            return;
        }

        var target = cave.GetRaw(idx + input.Direction) & 0x9F;
        switch (target)
        {
            case 11:
                if (state.JewelsCollected < state.JewelQuota)
                {
                    break;
                }

                state.IsCaveEnded = true;
                state.AdvanceToNextCave = true;
                state.EntranceProgress = 0;
                goto case 3;
            case 3:
                state.JewelsCollected++;
                if (state.JewelsCollected >= state.JewelQuota)
                {
                    state.CurrentJewelPoints = state.PointsPerJewelAfterQuota;
                }

                state.Score += state.CurrentJewelPoints;
                state.SoundEvents.Enqueue(SoundEvent.CollectJewel);
                goto case 0;
            case 0:
            case 1:
                if (target == 1)
                {
                    state.SoundEvents.Enqueue(SoundEvent.WalkEarth);
                }
                else if (target == 0)
                {
                    state.SoundEvents.Enqueue(SoundEvent.WalkEmpty);
                }

                cave.SetRaw(idx + input.Direction, (byte)(0x86 ^ input.GrabModifier));
                cave.SetRaw(idx, (byte)(0x80 ^ input.GrabModifier));
                break;
            case 2:
                // Schieben nach BD1 (BDCFF-Objektspezifikation 0006): nur waagerecht, nur RUHENDE
                // Boulder ("he cannot push falling boulders"), und dann mit einer Chance von 1:8 pro
                // Versuch. Der Wurf steht bewusst hinter den geometrischen Prüfungen — gewürfelt wird
                // nur, wenn Rockford es tatsächlich versucht ("each frame that he tries").
                // Das DOS-Original nutzte stattdessen ein festes Clk4-Fenster (jeder 2. Scan) und ließ
                // auch fallende Boulder schieben.
                if ((input.Direction == 1 || input.Direction == -1) &&
                    (cave.GetRaw(idx + input.Direction) & 0x40) == 0 &&
                    cave.GetRaw(idx + (input.Direction * 2)) == 0 &&
                    _random.Next(8) == 0)
                {
                    cave.SetRaw(idx + input.Direction, (byte)(0x86 ^ input.GrabModifier));
                    cave.SetRaw(idx + (input.Direction * 2), 0x82);
                    cave.SetRaw(idx, (byte)(0x80 ^ input.GrabModifier));
                    state.SoundEvents.Enqueue(SoundEvent.PushBoulder);
                }

                break;
        }
    }

    /// <summary>explosion(): 3x3-Bereich, Stahl bleibt verschont (:709-721).</summary>
    private static void Explode(Cave cave, GameState state, int centerIdx, byte anim)
    {
        var width = cave.Width;
        ReadOnlySpan<int> offsets =
        [
            -width - 1, -width, -width + 1,
            -1, 0, 1,
            width - 1, width, width + 1,
        ];

        foreach (var offset in offsets)
        {
            var target = centerIdx + offset;
            if (cave.GetRaw(target) != 5)
            {
                cave.SetRaw(target, anim);
            }
        }

        state.WechselExplo = 1;
        state.SoundEvents.Enqueue(SoundEvent.Explosion);
    }

    /// <summary>anfang(): Eingangsaufbau — Explosion bei 92, Rockford-Spawn bei 99 (:667-677).
    /// Die Türblinken-Animation selbst ist rein optisch und liegt in der Rendering-Schicht.</summary>
    public static void Entrance(Cave cave, GameState state, int entranceIndex)
    {
        if (state.EntranceProgress == 92)
        {
            cave.SetRaw(entranceIndex, 12);
            state.WechselExplo = 1;
            state.SoundEvents.Enqueue(SoundEvent.EntranceExplosion);
        }

        if (state.EntranceProgress == 99)
        {
            cave.SetRaw(entranceIndex, 6);
        }

        state.EntranceProgress++;
    }

    /// <summary>ende(): Palettenfarbe 0 blitzt einmal hell auf und bleibt danach dunkel (:681-687).</summary>
    public static void Exit(GameState state)
    {
        if (!state.ExitFlashOn)
        {
            state.PaletteColor0Override = Palette.ExitFlashBright;
            state.ExitFlashOn = true;
            state.SoundEvents.Enqueue(SoundEvent.EscapeDoorOpen);
        }
        else
        {
            state.PaletteColor0Override = Palette.ExitFlashDark;
        }
    }
}
