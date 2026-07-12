namespace BoulderDash.Core.Simulation;

/// <summary>
/// Ein Tick entspricht einem Durchlauf der Original-Timer-ISR
/// (timer_interrupt_service_routine, src/BOULDER.CPP:222-265) — enthält nur die
/// spielrelevanten Teile (Zähler, Kamera-Scroll, Countdowns, Physik, Eingang/Ausgang,
/// Dissolve). Die eigentliche Sprite-Auswahl (welches Frame gezeichnet wird) liegt in der
/// Rendering-Schicht (CaveRenderer), aber die zugrundeliegenden Zähler (wechsel_vier,
/// wechsel_boulder, wechsel_explo) laufen hier im Tick mit — im Original takten
/// boulder_lauf()/sprites_wechsel() im selben ISR-Aufruf wie die Physik, ihre Geschwindigkeit
/// ist daher an die Tickrate gekoppelt, nicht an die Bildwiederholrate.
/// </summary>
public sealed class GameTick
{
    private readonly CavePhysics _physics;
    private readonly ScreenCover _cover;

    public GameTick(CavePhysics physics, ScreenCover cover)
    {
        _physics = physics;
        _cover = cover;
    }

    public void Tick(Cave cave, GameState state, InputState input, Camera camera, Clocks clocks, int entranceIndex)
    {
        clocks.Tick();
        camera.Step(cave.Width, cave.Height);

        // boulder_lauf() (:611-646): Laufzyklus-Zähler, nur während aktiver Richtung.
        // Original nutzt hier (anders als die clk_*-Zähler!) zwei getrennte Anweisungen —
        // erst unbedingtes Inkrement, dann Prüfung des NEUEN Werts — macht Periode 6 (0..5),
        // nicht das Postfix-in-Bedingung-Muster der Clocks (das Periode 7 ergäbe).
        if (input.Direction != 0)
        {
            state.WechselBoulder++;
            if (state.WechselBoulder > 5)
            {
                state.WechselBoulder = 0;
            }
        }

        // sprites_wechsel() (:593-607): gemeinsamer Animationstakt Periode 8.
        if (state.WechselVier++ > 6)
        {
            state.WechselVier = 0;
        }

        if (state.WechselExplo > 0)
        {
            state.WechselExplo++;
        }

        if (state.WechselExplo == 8)
        {
            state.WechselExplo = 7;
        }

        // "pause" existiert im Original nur als nie gesetzter toter Code — hier weggelassen.
        if (clocks.Clk18 == 0 && !state.IsCaveEnded)
        {
            if (state.EntranceProgress > 99)
            {
                if (state.CaveTimeRemaining > 0)
                {
                    state.CaveTimeRemaining--;
                    if (state.CaveTimeRemaining <= 9)
                    {
                        state.SoundEvents.Enqueue(SoundEvent.TimeWarning);
                    }
                }
                else
                {
                    // Die Nullsekunde wird noch ganz ausgespielt: Schluss ist erst beim FOLGENDEN
                    // Sekundentakt. Die Anzeige steht dann schon eine Spielsekunde lang auf 000 und
                    // Rockford kann in dieser Zeit noch ziehen — genau darin liegt das BD1-Fenster für
                    // den Bonusüberlauf (Ausgang bei Zeit 0 -> Zähler läuft auf 255, siehe
                    // GameSession.BeginLevelEndBonus). Das DOS-Original beendete die Cave sofort bei 0
                    // (BOULDER.CPP:251) und kannte den Quirk deshalb nicht.
                    state.IsCaveEnded = true;
                }
            }

            if (state.EnchantedWallRunning && state.EnchantedWallTimeRemaining > 0)
            {
                state.EnchantedWallTimeRemaining--;
            }

            // Die Amoeba-Zeit läuft wie die Zaubermauer-Zeit in Spielsekunden, also tempo-unabhängig
            // (siehe CaveSpeed) — nach ihrem Ablauf wächst die Amoeba schnell (CavePhysics.ProcessAmoeba).
            if (state.EntranceProgress > 99 && state.AmoebaSlowGrowthRemaining > 0)
            {
                state.AmoebaSlowGrowthRemaining--;
            }
        }

        if (state.EnchantedWallTimeRemaining == 0)
        {
            state.EnchantedWallRunning = false;
        }

        if (!state.IsCaveEnded)
        {
            // Anders als im Original (dort "anfang_var>65", BOULDER.CPP:255) läuft die Cave schon
            // während des Aufdeckens ganz normal weiter — BD1-Verhalten, siehe ScreenCover.
            // Ungefährlich vor Rockfords Geburt: Regel() erkennt einen Tod erst ab EntranceProgress>100.
            if (clocks.Clk1 == 0)
            {
                _physics.Regel(cave, state, input, camera);
            }

            if (state.EntranceProgress < 101)
            {
                CavePhysics.Entrance(cave, state, entranceIndex);
            }

            if (state.JewelsCollected >= state.JewelQuota)
            {
                CavePhysics.Exit(state);
            }
        }

        // Auf-/Zudeck-Animation (ScreenCover): eine Runde pro Tick, dazu der Uncover-Sound, der
        // solange alle anderen Sounds übertönt (siehe AudioPlayer).
        if (_cover.IsActive)
        {
            _cover.Tick();
            state.SoundEvents.Enqueue(SoundEvent.Uncover);
        }

        state.ScreenCoverActive = _cover.IsActive;
    }
}
