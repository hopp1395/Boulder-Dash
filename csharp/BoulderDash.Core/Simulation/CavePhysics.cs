namespace BoulderDash.Core.Simulation;

/// <summary>
/// Byte-genaue Transliteration von regel() (src/BOULDER.CPP:725-959), explosion() (:709-721),
/// anfang() (:667-677) und ende() (:681-687). Arbeitet bewusst direkt auf rohen Kachelbytes statt
/// auf Element/Flag-Typen, weil die Original-Masken quer durch Bitgruppen schneiden (siehe
/// Cave-Klassenkommentar). Jede Regel ist einzeln mit der Original-Zeilennummer kommentiert.
/// </summary>
public sealed class CavePhysics
{
    private readonly BorlandRandom _random;

    public CavePhysics(BorlandRandom random)
    {
        _random = random;
    }

    /// <summary>Kachel ist Leer(0) oder Erde(1), ohne Flags — die wiederkehrende Prüfung "(x&amp;0xFE)==0".</summary>
    private static bool IsEmptyOrEarthRaw(byte raw) => (raw & 0xFE) == 0;

    public void Regel(Cave cave, GameState state, InputState input, Camera camera, Clocks clocks)
    {
        var width = cave.Width;
        var height = cave.Height;
        byte lavaVar = 0;
        var lavaNr = (byte)((_random.Next() % 96) + 1);
        byte lf = 3;

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
                ProcessAmoeba(cave, idx, width, ref lavaVar, lavaNr);
                ProcessButterfly(cave, state, idx, width);
                ProcessGhost(cave, state, idx, width);
                ProcessBoulderOrJewel(cave, state, idx, width);
                ProcessRockford(cave, state, input, camera, clocks, idx, width, height, col, row);
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

        // Lava-Einschluss prüfen: wächst irgendwo noch? (:937-947)
        for (var i = 0; i < width * height; i++)
        {
            if (cave.GetRaw(i) == 7)
            {
                if (IsEmptyOrEarthRaw(cave.GetRaw(i - width)) ||
                    IsEmptyOrEarthRaw(cave.GetRaw(i + width)) ||
                    IsEmptyOrEarthRaw(cave.GetRaw(i - 1)) ||
                    IsEmptyOrEarthRaw(cave.GetRaw(i + 1)))
                {
                    lf = 2;
                }
            }
        }

        // Eingeschlossen (lf==3, alles zu Jewel) oder überwuchert (>95 Zellen, alles zu Boulder) (:949-956)
        if (lavaVar > 95 || lf == 3)
        {
            for (var i = 0; i < width * height; i++)
            {
                if (cave.GetRaw(i) == 7)
                {
                    cave.SetRaw(i, lf);
                }
            }
        }
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

    /// <summary>Amoeba/Lava: pro Sweep breitet sich nur die zufällig gewählte lavaNr-te Zelle aus (:745-755).</summary>
    private static void ProcessAmoeba(Cave cave, int idx, int width, ref byte lavaVar, byte lavaNr)
    {
        var raw = cave.GetRaw(idx);
        if ((raw & 0x9F) != 0x07)
        {
            return;
        }

        lavaVar++;
        if (lavaVar != lavaNr)
        {
            return;
        }

        if (IsEmptyOrEarthRaw(cave.GetRaw(idx - width)))
        {
            cave.SetRaw(idx - width, 0x87);
        }
        else if (IsEmptyOrEarthRaw(cave.GetRaw(idx + width)))
        {
            cave.SetRaw(idx + width, 0x87);
        }
        else if (IsEmptyOrEarthRaw(cave.GetRaw(idx - 1)))
        {
            cave.SetRaw(idx - 1, 0x87);
        }
        else if (IsEmptyOrEarthRaw(cave.GetRaw(idx + 1)))
        {
            cave.SetRaw(idx + 1, 0x87);
        }
    }

    /// <summary>Schmetterling/Butterfly: explodiert zu Jewels bei Kontakt zu Rockford/Amoeba oder
    /// einem fallenden Boulder/Jewel von oben, sonst Wandfolge-Automat (:758-798).</summary>
    private static void ProcessButterfly(Cave cave, GameState state, int idx, int width)
    {
        var raw = cave.GetRaw(idx);
        if ((raw & 0x8F) != 9)
        {
            return;
        }

        if ((cave.GetRaw(idx - width) & 0xFE) == 6 ||
            (cave.GetRaw(idx + width) & 0xFE) == 6 ||
            (cave.GetRaw(idx - 1) & 0xFE) == 6 ||
            (cave.GetRaw(idx + 1) & 0xFE) == 6 ||
            (cave.GetRaw(idx - width) & 0x4C) == 0x40)
        {
            Explode(cave, state, idx, 0xCE);
            return;
        }

        for (var w = 0; w < 4; w++)
        {
            var current = cave.GetRaw(idx);
            switch (current & 0x60)
            {
                case 0x00:
                    if (cave.GetRaw(idx - width) == 0)
                    {
                        cave.SetRaw(idx, (byte)((current & 0x99) | 0xA9));
                        w = 5;
                    }
                    else if (cave.GetRaw(idx - 1) == 0)
                    {
                        w = 5;
                    }
                    else
                    {
                        cave.SetRaw(idx, 0x39);
                    }

                    break;
                case 0x20:
                    if (cave.GetRaw(idx + 1) == 0)
                    {
                        cave.SetRaw(idx, (byte)((current & 0x99) | 0xC9));
                        w = 5;
                    }
                    else if (cave.GetRaw(idx - width) == 0)
                    {
                        w = 5;
                    }
                    else
                    {
                        cave.SetRaw(idx, 0x59);
                    }

                    break;
                case 0x40:
                    if (cave.GetRaw(idx + width) == 0)
                    {
                        cave.SetRaw(idx, (byte)((current & 0x99) | 0xE9));
                        w = 5;
                    }
                    else if (cave.GetRaw(idx + 1) == 0)
                    {
                        w = 5;
                    }
                    else
                    {
                        cave.SetRaw(idx, 0x79);
                    }

                    break;
                case 0x60:
                    if (cave.GetRaw(idx - 1) == 0)
                    {
                        cave.SetRaw(idx, (byte)((current & 0x99) | 0x89));
                        w = 5;
                    }
                    else if (cave.GetRaw(idx + width) == 0)
                    {
                        w = 5;
                    }
                    else
                    {
                        cave.SetRaw(idx, 0x19);
                    }

                    break;
            }
        }

        var afterLoop = cave.GetRaw(idx);
        switch (afterLoop & 0x70)
        {
            case 0x00:
                cave.SetRaw(idx - 1, 0x89);
                cave.SetRaw(idx, 0);
                break;
            case 0x20:
                cave.SetRaw(idx - width, 0xA9);
                cave.SetRaw(idx, 0);
                break;
            case 0x40:
                cave.SetRaw(idx + 1, 0xC9);
                cave.SetRaw(idx, 0);
                break;
            case 0x60:
                cave.SetRaw(idx + width, 0xE9);
                cave.SetRaw(idx, 0);
                break;
        }

        var final = (byte)(cave.GetRaw(idx) | 0x80);
        final &= 0xEF;
        cave.SetRaw(idx, final);
    }

    /// <summary>Geist/Firefly: spiegelbildliche Logik zu Butterfly, explodiert zu Leere statt Jewels (:801-840).</summary>
    private static void ProcessGhost(Cave cave, GameState state, int idx, int width)
    {
        var raw = cave.GetRaw(idx);
        if ((raw & 0x8F) != 8)
        {
            return;
        }

        if ((cave.GetRaw(idx - width) & 0x7E) == 6 ||
            (cave.GetRaw(idx + width) & 0x7E) == 6 ||
            (cave.GetRaw(idx - 1) & 0x7E) == 6 ||
            (cave.GetRaw(idx + 1) & 0x7E) == 6 ||
            (cave.GetRaw(idx - width) & 0x42) == 0x42)
        {
            Explode(cave, state, idx, 0xCC);
            return;
        }

        for (var w = 0; w < 4; w++)
        {
            var current = cave.GetRaw(idx);
            switch (current & 0x60)
            {
                case 0x00:
                    if (cave.GetRaw(idx + width) == 0)
                    {
                        cave.SetRaw(idx, (byte)((current & 0x99) | 0xE8));
                        w = 5;
                    }
                    else if (cave.GetRaw(idx - 1) == 0)
                    {
                        w = 5;
                    }
                    else
                    {
                        cave.SetRaw(idx, 0x38);
                    }

                    break;
                case 0x20:
                    if (cave.GetRaw(idx - 1) == 0)
                    {
                        cave.SetRaw(idx, (byte)((current & 0x99) | 0x88));
                        w = 5;
                    }
                    else if (cave.GetRaw(idx - width) == 0)
                    {
                        w = 5;
                    }
                    else
                    {
                        cave.SetRaw(idx, 0x58);
                    }

                    break;
                case 0x40:
                    if (cave.GetRaw(idx - width) == 0)
                    {
                        cave.SetRaw(idx, (byte)((current & 0x99) | 0xA8));
                        w = 5;
                    }
                    else if (cave.GetRaw(idx + 1) == 0)
                    {
                        w = 5;
                    }
                    else
                    {
                        cave.SetRaw(idx, 0x78);
                    }

                    break;
                case 0x60:
                    if (cave.GetRaw(idx + 1) == 0)
                    {
                        cave.SetRaw(idx, (byte)((current & 0x99) | 0xC8));
                        w = 5;
                    }
                    else if (cave.GetRaw(idx + width) == 0)
                    {
                        w = 5;
                    }
                    else
                    {
                        cave.SetRaw(idx, 0x18);
                    }

                    break;
            }
        }

        var afterLoop = cave.GetRaw(idx);
        switch (afterLoop & 0x70)
        {
            case 0x00:
                cave.SetRaw(idx - 1, 0x88);
                cave.SetRaw(idx, 0);
                break;
            case 0x20:
                cave.SetRaw(idx - width, 0xA8);
                cave.SetRaw(idx, 0);
                break;
            case 0x40:
                cave.SetRaw(idx + 1, 0xC8);
                cave.SetRaw(idx, 0);
                break;
            case 0x60:
                cave.SetRaw(idx + width, 0xE8);
                cave.SetRaw(idx, 0);
                break;
        }

        var final = (byte)(cave.GetRaw(idx) | 0x80);
        final &= 0xEF;
        cave.SetRaw(idx, final);
    }

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
                if (state.EnchantedWallTimeRemaining > 0)
                {
                    if ((cave.GetRaw(idx) & 0x40) == 0x40)
                    {
                        state.EnchantedWallRunning = true;
                        if (cave.GetRaw(idx + (2 * width)) == 0)
                        {
                            var minusOne = (byte)(cave.GetRaw(idx) - 1);
                            cave.SetRaw(idx + (2 * width), (byte)(minusOne | 0xC2));
                        }

                        cave.SetRaw(idx, 0x80);
                    }
                }
                else if ((cave.GetRaw(idx) & 0x40) == 0x40)
                {
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
    private static void ProcessRockford(
        Cave cave, GameState state, InputState input, Camera camera, Clocks clocks,
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
                if (cave.GetRaw(idx + (input.Direction * 2)) == 0 && clocks.Clk4 == 0)
                {
                    if (input.Direction == 1 || input.Direction == -1)
                    {
                        cave.SetRaw(idx + input.Direction, (byte)(0x86 ^ input.GrabModifier));
                        cave.SetRaw(idx + (input.Direction * 2), 0x82);
                        cave.SetRaw(idx, (byte)(0x80 ^ input.GrabModifier));
                        state.SoundEvents.Enqueue(SoundEvent.PushBoulder);
                    }
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
