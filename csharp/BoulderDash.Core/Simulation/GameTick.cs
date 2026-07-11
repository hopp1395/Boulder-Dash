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
    private readonly Dissolve _dissolve;

    public GameTick(CavePhysics physics, Dissolve dissolve)
    {
        _physics = physics;
        _dissolve = dissolve;
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
            if (state.EntranceProgress > 99 && state.CaveTimeRemaining > 0)
            {
                state.CaveTimeRemaining--;
                if (state.CaveTimeRemaining <= 9)
                {
                    state.SoundEvents.Enqueue(SoundEvent.TimeWarning);
                }
            }

            if (state.EnchantedWallRunning && state.EnchantedWallTimeRemaining > 0)
            {
                state.EnchantedWallTimeRemaining--;
            }
        }

        if (state.EnchantedWallTimeRemaining == 0)
        {
            state.EnchantedWallRunning = false;
        }

        if (state.CaveTimeRemaining == 0)
        {
            state.IsCaveEnded = true;
        }

        if (!state.IsCaveEnded)
        {
            if (clocks.Clk1 == 0 && state.EntranceProgress > 65)
            {
                _physics.Regel(cave, state, input, camera, clocks);
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

        if (state.EntranceProgress < 65 && !state.IsCaveEnded)
        {
            _dissolve.Tick(state.EntranceProgress);
            state.SoundEvents.Enqueue(SoundEvent.Uncover);
        }
    }
}
