using BoulderDash.Core.Objects;

namespace BoulderDash.Core.Simulation;

/// <summary>
/// Der Cave-Scan: das Äquivalent von regel() (src/BOULDER.CPP:725-959), explosion() (:709-721),
/// anfang() (:667-677) und ende() (:681-687). Er geht das Gitter zeilenweise durch und lässt jedes
/// Objekt sein Verhalten ausspielen.
///
/// Die Reihenfolge der Handler je Zelle ist verhaltensrelevant und entspricht der des Originals.
/// Ebenso die Zeilenrichtung: Weil von oben nach unten gescannt wird, würde ein fallendes Objekt
/// seiner eigenen Bewegung hinterherfallen — das verhindert das Verarbeitet-Flag
/// (<see cref="CaveObject.Scanned"/>), das jeder Handler setzt und der Scan am Ende wieder löscht.
/// </summary>
public sealed class CavePhysics
{
    /// <summary>Ab dieser Zellzahl wird die ganze Amoeba zu Steinen. Feste BD1-Konstante (in BD1
    /// nicht pro Cave einstellbar); das DOS-Original wandelte schon ab 96 Zellen um (BOULDER.CPP:951).</summary>
    private const int AmoebaMaxSize = 200;

    private readonly Random _random;

    public CavePhysics(Random random)
    {
        _random = random;
    }

    public void Regel(Cave cave, GameState state, InputState input, Camera camera)
    {
        var width = cave.Width;
        var height = cave.Height;

        // Die Amoeba entscheidet ihr Schicksal aus den Zahlen des VORIGEN Scans — laut Spezifikation
        // wirken "zu groß" und "eingeschlossen" jeweils erst im Folge-Scan, und "zu groß" hat Vorrang.
        Element? amoebaFate = null;
        if (state.AmoebaCountLastScan >= AmoebaMaxSize)
        {
            amoebaFate = Element.Boulder;
        }
        else if (state.AmoebaSuffocatedLastScan)
        {
            amoebaFate = Element.Jewel;
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

                ResolveExplosion(cave, idx);
                ProcessAmoeba(cave, state, idx, width, amoebaFate, ref amoebaFound, ref amoebaCanGrow);
                ProcessCreature(cave, state, idx, width);
                ProcessFallingObject(cave, state, idx, width);
                ProcessRockford(cave, state, input, camera, idx, width, height, col, row);
            }
        }

        if (wasAlive && state.Stat != 0)
        {
            state.SoundEvents.Enqueue(SoundEvent.Death);
        }

        cave.ClearScanned();

        state.AmoebaCountLastScan = amoebaFound;
        state.AmoebaSuffocatedLastScan = amoebaFound > 0 && !amoebaCanGrow;

        // Für die Amoeba-Drone (kein Original-Äquivalent, siehe AudioPlayer/SoundRecipes).
        state.AmoebaPresent = amoebaFound > 0;
    }

    /// <summary>Eine ausgelaufene Explosion gibt ihre Kachel frei — der Geist hinterlässt Leerraum,
    /// der Schmetterling einen Diamanten (:740-743).</summary>
    private static void ResolveExplosion(Cave cave, int idx)
    {
        if (cave.Get(idx) is ExplosionObject { ExplosionPhase: ExplosionObject.FinalPhase } explosion)
        {
            cave.Spawn(idx, explosion.Remnant());
        }
    }

    /// <summary>
    /// Amoeba nach BD1 (BDCFF 000A): Jede Zelle würfelt pro Scan einzeln, ob sie wächst — mit 4/128
    /// (~3 %), nach Ablauf der Amoeba-Zeit mit 4/16 (25 %) —, und wächst dann in genau eine zufällig
    /// gezogene der vier Richtungen, sofern dort Leerraum oder Erde liegt. Wird sie zu groß oder
    /// erstickt sie, wandelt der Folge-Scan sie über <paramref name="fate"/> um.
    /// Das DOS-Original ließ pro Scan nur eine einzige, per rand()%96 gewählte Zelle wachsen, und die
    /// immer in fester Richtungspriorität (BOULDER.CPP:745-755).
    /// </summary>
    private void ProcessAmoeba(
        Cave cave, GameState state, int idx, int width, Element? fate, ref int found, ref bool canGrow)
    {
        if (cave.Get(idx) is not AmoebaObject { Scanned: false })
        {
            return;
        }

        if (fate is { } destiny)
        {
            // Mit Verarbeitet-Flag, damit ein so entstandener Stein nicht schon im selben Scan
            // weiterfällt (dasselbe Muster wie bei der auslaufenden Explosion).
            var converted = cave.Create(destiny);
            converted.Scanned = true;
            cave.Spawn(idx, converted);
            return;
        }

        found++;

        ReadOnlySpan<int> directions = [-width, width, -1, 1];
        foreach (var direction in directions)
        {
            if (cave.Get(idx + direction).CanAmoebaGrowInto)
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
        if (cave.Get(target).CanAmoebaGrowInto)
        {
            // Mit Verarbeitet-Flag: Sie wächst erst im nächsten Scan weiter und zählt auch erst dann
            // mit ("newly grown amoeba cannot expand until the following frame").
            cave.Spawn(target, new AmoebaObject { Scanned = true });
        }
    }

    /// <summary>
    /// Geist und Schmetterling (BDCFF 0008/0009): Sie versuchen zuerst ihre Vorzugsdrehung, sonst
    /// geradeaus. Ist beides versperrt, drehen sie sich genau EINMAL zur Gegenseite und ziehen in
    /// diesem Scan nicht. Berührung mit Rockford oder Amoeba — oder ein fallendes Objekt direkt
    /// darüber — sprengt sie.
    ///
    /// Original: BOULDER.CPP:758-840. Zwei Abweichungen sind dort einzeln kommentiert (die
    /// Blickrichtung nach Blockade und die Kontaktmaske); hier kommt hinzu, dass beide Kreaturen
    /// denselben Handler durchlaufen statt zwei getrennter — sie teilen sich ohnehin jede Regel und
    /// unterscheiden sich nur in Drehsinn und Explosionsart.
    /// </summary>
    private static void ProcessCreature(Cave cave, GameState state, int idx, int width)
    {
        if (cave.Get(idx) is not CreatureObject { Scanned: false } creature)
        {
            return;
        }

        // Rockford und Amoeba zünden ringsum; ein fallender Stein/Diamant zündet von oben.
        // Ein Rockford bzw. eine Amoeba, die sich in DIESEM Scan schon bewegt hat, zündet ebenfalls
        // — die Spezifikation zählt "Rockford, scanned this frame" ausdrücklich zu den Auslösern.
        // Das DOS-Original prüfte hier beim Schmetterling (anders als beim Geist) mit 0xFE und
        // übersah diesen Fall.
        if (cave.Get(idx - width).TriggersCreature ||
            cave.Get(idx + width).TriggersCreature ||
            cave.Get(idx - 1).TriggersCreature ||
            cave.Get(idx + 1).TriggersCreature ||
            cave.Get(idx - width) is FallingObject { Falling: true })
        {
            Explode(cave, state, idx, creature.CreateExplosion);
            return;
        }

        if (cave.Get(idx + creature.PreferredFacing.Offset(width)).IsFreeSpace)
        {
            MoveCreature(cave, idx, width, creature, creature.PreferredFacing);
        }
        else if (cave.Get(idx + creature.Facing.Offset(width)).IsFreeSpace)
        {
            MoveCreature(cave, idx, width, creature, creature.Facing);
        }
        else
        {
            // Beides versperrt: einmal zur Gegenseite drehen, aber stehen bleiben.
            creature.Facing = creature.BlockedFacing;
            creature.Scanned = true;
        }
    }

    /// <summary>Setzt die Kreatur ins Nachbarfeld und räumt ihr altes.</summary>
    private static void MoveCreature(Cave cave, int idx, int width, CreatureObject creature, CreatureFacing facing)
    {
        creature.Facing = facing;
        creature.Scanned = true;

        cave.Set(idx + facing.Offset(width), creature);
        cave.Spawn(idx, new EmptyObject { Scanned = true });
    }

    /// <summary>Stein/Diamant: fällt, rollt ab, wandelt sich an der Zaubermauer, erschlägt Rockford
    /// beim Landen (:842-887).</summary>
    private static void ProcessFallingObject(Cave cave, GameState state, int idx, int width)
    {
        if (cave.Get(idx) is not FallingObject { Scanned: false } falling)
        {
            return;
        }

        var belowIdx = idx + width;
        var below = cave.Get(belowIdx);

        switch (below)
        {
            // Fallen. Anders als beim Abrollen (unten) genügt hier ein Leerraum, der in diesem Scan
            // schon verarbeitet wurde — das Original maskiert an dieser Stelle bewusst nur die
            // Element-ID heraus und übersieht das Verarbeitet-Flag.
            case EmptyObject:
                falling.Falling = true;
                falling.Scanned = true;
                cave.Set(belowIdx, falling);
                cave.Spawn(idx, new EmptyObject { Scanned = true });
                break;

            // Abrollen von einer Rundung — der Mauer oder einem RUHENDEN Stein/Diamanten.
            case { IsRounded: true }:
                RollOff(cave, state, idx, width, falling);
                break;

            case EnchantedWallObject:
                HitEnchantedWall(cave, state, idx, width, falling);
                break;

            // Nur ein FALLENDES Objekt erschlägt Rockford — ein ruhendes liegt einfach auf ihm.
            case RockfordObject when falling.Falling:
                Explode(cave, state, belowIdx, static () => new ExplosionObject());
                break;

            // Auf einer Kreatur bleibt es liegen: Sie zündet sich beim eigenen Zug selbst.
            case CreatureObject:
                break;

            default:
                Land(state, falling);
                break;
        }
    }

    /// <summary>Rollt zur Seite ab, wenn dort UND darunter unberührter Leerraum ist — sonst landet
    /// das Objekt. Links hat Vorrang vor rechts.</summary>
    private static void RollOff(Cave cave, GameState state, int idx, int width, FallingObject falling)
    {
        foreach (var side in (ReadOnlySpan<int>)[-1, 1])
        {
            if (!cave.Get(idx + side).IsFreeSpace || !cave.Get(idx + side + width).IsFreeSpace)
            {
                continue;
            }

            // Das Fall-Momentum bleibt dabei, wie es ist: Ein ruhender Stein rollt ab, ohne schon zu
            // fallen — erst im Folge-Scan hat er Leerraum unter sich und fängt an.
            falling.Scanned = true;
            cave.Set(idx + side, falling);
            cave.Spawn(idx, new EmptyObject { Scanned = true });

            // Die Zelle unter dem Ziel wird gesperrt, damit in diesem Scan nichts hineinfällt.
            cave.Get(idx + side + width).Scanned = true;
            return;
        }

        Land(state, falling);
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
    private static void HitEnchantedWall(Cave cave, GameState state, int idx, int width, FallingObject falling)
    {
        // Ein ruhendes Objekt liegt einfach auf der Mauer.
        if (!falling.Falling)
        {
            return;
        }

        var product = falling.EnchantedWallProduct();
        state.SoundEvents.Enqueue(product.LandingSound);

        if (state.EnchantedWallTimeRemaining == 0)
        {
            // Abgelaufene (oder nie aktivierte) Mauer: das Objekt verschwindet ersatzlos. Die
            // geräumte Zelle bleibt dabei UNverarbeitet — anders als in allen übrigen Zweigen.
            cave.Spawn(idx, new EmptyObject());
            return;
        }

        state.EnchantedWallRunning = true;

        var exitIdx = idx + (2 * width);
        if (cave.Get(exitIdx).IsFreeSpace)
        {
            product.Scanned = true;
            cave.Spawn(exitIdx, product);
        }

        // Steht unter der Mauer etwas im Weg, wird das Objekt trotzdem gelöscht:
        // "the boulder or diamond is lost" (BDCFF 0002).
        cave.Spawn(idx, new EmptyObject { Scanned = true });
    }

    /// <summary>Kommt zur Ruhe. Der Aufschlag klingt nur, wenn das Objekt wirklich fiel — ein bereits
    /// ruhendes löst kein Sound-Ereignis aus.</summary>
    private static void Land(GameState state, FallingObject falling)
    {
        if (!falling.Falling)
        {
            return;
        }

        state.SoundEvents.Enqueue(falling.LandingSound);
        falling.Falling = false;
    }

    /// <summary>Rockford: Kamera-Scroll-Auslöser plus Bewegung/Graben/Sammeln/Schieben (:890-923).
    /// Die vier Kamerabedingungen setzen nur das Scroll-Ziel und beeinflussen die Bewegung nicht.
    /// Abweichung vom Original (:893-896): Die vier Schwellen liegen je eine Kachel weiter innen,
    /// damit das Scrollen einen Schritt früher einsetzt. Schwellen und Scrollweiten leiten sich aus
    /// der Sichtfenstergröße ab (siehe ViewportSize) und sind beim Original-Sichtfenster 20x12
    /// identisch mit den dortigen Konstanten (16/8/7/5); zeigt das Sichtfenster die ganze Cave,
    /// greifen die Wächter und es wird gar nicht gescrollt.
    /// Im DOS-Original hing die Bewegungsverarbeitung durch ein Dangling-Else an der vierten
    /// Bedingung: löste Rockford den Aufwärtsscroll aus, blieb seine Bewegung den ganzen Scan über
    /// aus — er hakte sichtbar. Ein reiner Programmierfehler ohne BD1-Entsprechung, hier behoben.</summary>
    private void ProcessRockford(
        Cave cave, GameState state, InputState input, Camera camera,
        int idx, int width, int height, int col, int row)
    {
        if (cave.Get(idx) is not RockfordObject { Scanned: false } rockford)
        {
            return;
        }

        state.Stat = 0;

        var viewport = camera.Viewport;

        if (camera.X + viewport.ScrollTriggerRight < col && camera.X < width - viewport.Columns)
        {
            camera.Relx = (sbyte)viewport.ScrollAmountX;
        }

        if (camera.X + ViewportSize.ScrollTriggerNear == col && camera.X > 0)
        {
            camera.Relx = (sbyte)-viewport.ScrollAmountX;
        }

        if (camera.Y + viewport.ScrollTriggerBottom < row && camera.Y < height - viewport.Rows)
        {
            camera.Rely = (sbyte)viewport.ScrollAmountY;
        }

        if (camera.Y + ViewportSize.ScrollTriggerNear == row && camera.Y > 0)
        {
            camera.Rely = (sbyte)-viewport.ScrollAmountY;
        }

        var targetIdx = idx + input.Direction;
        var target = cave.Get(targetIdx);

        // Eine in diesem Scan schon verarbeitete Zelle ist unpassierbar. Im Original leistet das das
        // Verarbeitet-Bit, das die Zielmaske (anders als alle anderen) NICHT ausblendet — ein Detail,
        // das leicht zu übersehen ist, aber Rockford daran hindert, einer gerade geräumten Zelle
        // hinterherzuziehen.
        if (target.Scanned)
        {
            return;
        }

        switch (target)
        {
            case EscapeDoorObject when state.JewelsCollected >= state.JewelQuota:
                state.IsCaveEnded = true;
                state.AdvanceToNextCave = true;
                state.EntranceProgress = 0;

                // Rockford zieht nur in die Tür — der Ausgang ist KEIN Diamant. Das DOS-Original
                // sprang hier auf den Diamant-Zweig durch und wertete das Betreten des Ausgangs als
                // eingesammelten Diamanten (Zähler, Punkte und Sammel-Sound inklusive).
                MoveRockford(cave, input, idx, targetIdx, rockford);
                break;

            case JewelObject:
                state.JewelsCollected++;
                if (state.JewelsCollected >= state.JewelQuota)
                {
                    state.CurrentJewelPoints = state.PointsPerJewelAfterQuota;
                }

                state.Score += state.CurrentJewelPoints;
                state.SoundEvents.Enqueue(SoundEvent.CollectJewel);
                MoveRockford(cave, input, idx, targetIdx, rockford);
                break;

            case EarthObject:
                state.SoundEvents.Enqueue(SoundEvent.WalkEarth);
                MoveRockford(cave, input, idx, targetIdx, rockford);
                break;

            case EmptyObject:
                state.SoundEvents.Enqueue(SoundEvent.WalkEmpty);
                MoveRockford(cave, input, idx, targetIdx, rockford);
                break;

            case BoulderObject boulder:
                PushBoulder(cave, state, input, idx, targetIdx, rockford, boulder);
                break;
        }
    }

    /// <summary>
    /// Rockford zieht um — oder greift nur hinein. Beim Greifen räumt er die Zielzelle leer und
    /// bleibt selbst stehen; das Original erledigt beides mit demselben Code, indem es die
    /// Kachelbytes mit dem Greif-Modifikator XOR-verknüpft (0x86 ^ 6 == 0x80).
    /// </summary>
    private static void MoveRockford(Cave cave, InputState input, int idx, int targetIdx, RockfordObject rockford)
    {
        rockford.Scanned = true;

        if (input.IsGrabbing)
        {
            cave.Spawn(targetIdx, new EmptyObject { Scanned = true });
            return;
        }

        cave.Set(targetIdx, rockford);
        cave.Spawn(idx, new EmptyObject { Scanned = true });
    }

    /// <summary>
    /// Schieben nach BD1 (BDCFF 0006): nur waagerecht, nur RUHENDE Steine ("he cannot push falling
    /// boulders"), und dann mit einer Chance von 1:8 pro Versuch. Der Wurf steht bewusst hinter den
    /// geometrischen Prüfungen — gewürfelt wird nur, wenn Rockford es tatsächlich versucht ("each
    /// frame that he tries").
    /// Das DOS-Original nutzte stattdessen ein festes Clk4-Fenster (jeder 2. Scan) und ließ auch
    /// fallende Steine schieben.
    /// </summary>
    private void PushBoulder(
        Cave cave, GameState state, InputState input,
        int idx, int targetIdx, RockfordObject rockford, BoulderObject boulder)
    {
        var horizontal = input.Direction is 1 or -1;
        var behindIdx = idx + (input.Direction * 2);

        if (!horizontal || boulder.Falling || !cave.Get(behindIdx).IsFreeSpace || _random.Next(8) != 0)
        {
            return;
        }

        boulder.Scanned = true;
        cave.Set(behindIdx, boulder);
        MoveRockford(cave, input, idx, targetIdx, rockford);
        state.SoundEvents.Enqueue(SoundEvent.PushBoulder);
    }

    /// <summary>explosion(): sprengt den 3x3-Bereich frei; Stahlwand, Ein- und Ausgang bleiben stehen
    /// (:709-721). Jede getroffene Kachel bekommt ihre eigene Explosionsinstanz.</summary>
    private static void Explode(Cave cave, GameState state, int centerIdx, Func<ExplosionObject> create)
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
            if (cave.Get(target).IsExplosionProof)
            {
                continue;
            }

            var explosion = create();
            explosion.Scanned = true;
            cave.Spawn(target, explosion);
        }

        RestartExplosions(cave);
        state.SoundEvents.Enqueue(SoundEvent.Explosion);
    }

    /// <summary>Setzt ALLE Explosionen der Cave auf die erste Phase zurück — auch die, die anderswo
    /// schon halb abgelaufen sind. Im Original war wechsel_explo eine einzige globale Variable, und
    /// jede neue Explosion setzte sie auf 1; alle Explosionen verschwanden dadurch gemeinsam. Diese
    /// Kopplung ist bewusst erhalten (siehe ExplosionObject).</summary>
    private static void RestartExplosions(Cave cave)
    {
        for (var i = 0; i < cave.Width * cave.Height; i++)
        {
            if (cave.Get(i) is ExplosionObject explosion)
            {
                explosion.ExplosionPhase = 1;
            }
        }
    }

    /// <summary>anfang(): Eingangsaufbau — Explosion bei 92, Rockford bei 99 (:667-677).
    /// Die Türblinken-Animation selbst ist rein optisch und liegt beim EntranceObject.</summary>
    public static void Entrance(Cave cave, GameState state, int entranceIndex)
    {
        if (state.EntranceProgress == 92)
        {
            cave.Spawn(entranceIndex, new ExplosionObject());
            RestartExplosions(cave);
            state.SoundEvents.Enqueue(SoundEvent.EntranceExplosion);
        }

        if (state.EntranceProgress == 99)
        {
            cave.Spawn(entranceIndex, new RockfordObject());
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
