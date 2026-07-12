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
    private readonly Random _random;

    public GameTick(CavePhysics physics, ScreenCover cover, Random random)
    {
        _physics = physics;
        _cover = cover;
        _random = random;
    }

    public void Tick(Cave cave, GameState state, InputState input, Camera camera, Clocks clocks, int entranceIndex)
    {
        clocks.Tick();
        camera.Step(cave.Width, cave.Height);

        // sprites_wechsel()/boulder_lauf() (:593-646): Früher waren das drei globale Zähler
        // (wechsel_vier, wechsel_boulder, wechsel_explo). Jetzt schaltet jedes Objekt seinen eigenen
        // weiter — im selben Takt, also weiterhin synchron.
        cave.Animate(input);

        RollIdleAnimation(cave, input);

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
            // Die Physik startet erst nach dem Levelaufbau: Höhle komplett aufgedeckt UND das
            // Startsignal (die Eingangs-Explosion bei EntranceProgress==92, siehe Entrance())
            // ertönt — vorher fällt kein Stein und bewegt sich kein Gegner. Das DOS-Original
            // wartete ähnlich, nur mit anderer Schwelle ("anfang_var>65", BOULDER.CPP:255).
            // Beim ZUDECKEN am Cave-Ende (Covering) läuft die Physik dagegen weiter — wie im
            // Original die ISR bis zum Ende von game_start().
            var buildUpDone = _cover.Phase != ScreenCoverPhase.Uncovering && state.EntranceProgress > 92;
            if (clocks.Clk1 == 0 && buildUpDone)
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

    /// <summary>
    /// Rockfords Ruheanimation nach BD1 (BDCFF 0006): Zu Beginn jeder 8-Frame-Sequenz — also genau
    /// beim Umlauf des Animationstakts — wird für einen stehenden Rockford neu ausgewürfelt, ob er in
    /// dieser Sequenz blinzelt (1/4) und ob das Fußtappen umschaltet (1/16). Beides läuft unabhängig
    /// voneinander (auf dem C64 steuern es obere und untere Körperhälfte getrennt); in Bewegung tut
    /// er weder das eine noch das andere. Das DOS-Original hatte die Animation nie fertiggestellt —
    /// boulder_wait() (BOULDER.CPP:648-663) ist auskommentiert, seine Frames lagen brach.
    ///
    /// Gewürfelt wird hier und nicht im RockfordObject, und zwar auch dann, wenn Rockford noch gar
    /// nicht auf dem Feld steht: Der Zufallsstrom ist derselbe, aus dem die Amoeba ihr Wachstum
    /// zieht. Hinge die Zahl der Ziehungen daran, ob Rockford schon erschienen ist, verschöbe sich
    /// die gesamte Folge — und mit ihr das Verhalten der ganzen Cave.
    /// </summary>
    private void RollIdleAnimation(Cave cave, InputState input)
    {
        if (cave.AnimationPhase != 0 || input.Direction != 0)
        {
            return;
        }

        var blinking = _random.Next(4) == 0;
        var togglesTapping = _random.Next(16) == 0;

        if (cave.FindRockford() is not { } rockford)
        {
            return;
        }

        rockford.Blinking = blinking;
        if (togglesTapping)
        {
            rockford.Tapping = !rockford.Tapping;
        }
    }
}
