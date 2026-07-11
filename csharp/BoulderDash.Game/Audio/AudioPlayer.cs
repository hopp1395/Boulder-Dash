using BoulderDash.Core.Audio;
using BoulderDash.Core.Simulation;
using Microsoft.Xna.Framework.Audio;

namespace BoulderDash.Game.Audio;

/// <summary>
/// Synthetisiert die Original-C64-Soundeffekte UND die Titelmelodie (Peter Broadribb,
/// elmerproductions.com/sp/peterb/sounds.html — siehe Core/Audio/SoundRecipes.cs bzw.
/// Core/Audio/ThemeTune.cs) über SidSynth und spielt sie ab.
///
/// Vereinfachtes Drei-Stimmen-Modell (Original: SID hat 3 Hardware-Stimmen mit fester
/// Priorität "Stimme 3 = Crack &gt; Amoeba &gt; Zaubermauer, Stimme 2 = Bewegung, Stimme 1 =
/// alles andere, aktueller Sound spielt zu Ende"): hier feuert Stimme 1 jeden Einzelsound
/// unabhängig ab (MonoGame verwaltet überlappende SoundEffect-Wiedergabe selbst), da alle
/// Effekte sehr kurz sind (&lt;150ms) und ein hörbarer Unterschied zur strikten
/// "erst-fertig-dann-nächster"-Warteschlange kaum auffällt. Nur die beiden Dauerklänge
/// (Amoeba/Zaubermauer) laufen als echte Loop-Instanzen mit Start/Stop nach Spielzustand.
/// </summary>
public sealed class AudioPlayer
{
    private readonly Random _random = new();
    private readonly SoundEffectInstance _music;

    private readonly SoundEffect _collectJewel;
    private readonly SoundEffect _boulderLand;
    private readonly SoundEffect _explosion;
    private readonly SoundEffect _crack;
    private readonly SoundEffect _walkEmpty;
    private readonly SoundEffect _walkEarth;
    private readonly SoundEffect[] _timeWarningBySecond; // Index 0..9

    private readonly SoundEffectInstance _amoebaLoop;
    private readonly SoundEffectInstance _enchantedWallLoop;

    private int _bonusSweepZ = SoundRecipes.BonusSweepInitialZ;

    public AudioPlayer()
    {
        _music = BuildEffect(ThemeTune.Render()).CreateInstance();
        _music.IsLooped = true;

        _collectJewel = RenderTriangleEffect(SoundRecipes.CollectJewelHz, SoundRecipes.CollectJewel);
        _boulderLand = RenderNoiseEffect(SoundRecipes.BoulderLandHz, SoundRecipes.BoulderLand);
        _explosion = RenderNoiseEffect(SoundRecipes.ExplosionHz, SoundRecipes.Explosion);
        _crack = RenderNoiseEffect(SoundRecipes.CrackHz, SoundRecipes.Crack);
        _walkEmpty = RenderNoiseEffect(SoundRecipes.WalkEmptyHz, SoundRecipes.Movement);
        _walkEarth = RenderNoiseEffect(SoundRecipes.WalkEarthHz, SoundRecipes.Movement);

        _timeWarningBySecond = new SoundEffect[10];
        for (var second = 0; second <= 9; second++)
        {
            _timeWarningBySecond[second] = RenderTriangleEffect(SoundRecipes.TimeWarningHz(second), SoundRecipes.TimeWarning);
        }

        _amoebaLoop = BuildDroneLoop(SoundRecipes.AmoebaDrone, SoundRecipes.AmoebaDroneHzMin, SoundRecipes.AmoebaDroneHzMax).CreateInstance();
        _amoebaLoop.IsLooped = true;
        _enchantedWallLoop = BuildDroneLoop(SoundRecipes.EnchantedWallDrone, SoundRecipes.EnchantedWallDroneHzMin, SoundRecipes.EnchantedWallDroneHzMax).CreateInstance();
        _enchantedWallLoop.IsLooped = true;
    }

    /// <summary>Einmal pro Frame: leert die Ereigniswarteschlange, spielt Einzelsounds ab und
    /// setzt die beiden Dauerklänge (Amoeba/Zaubermauer) je nach aktuellem Spielzustand.
    /// Während des Auf-/Zudeckens übertönt der Uncover-Sound alle anderen (BD1) — die übrigen
    /// Ereignisse werden verworfen, die Dauerklänge schweigen.</summary>
    public void Update(GameState state)
    {
        var covering = state.ScreenCoverActive;

        while (state.SoundEvents.Count > 0)
        {
            var soundEvent = state.SoundEvents.Dequeue();
            if (covering && soundEvent != SoundEvent.Uncover)
            {
                continue;
            }

            Play(soundEvent, state);
        }

        SetLoopState(_amoebaLoop, !covering && state.AmoebaPresent && state.EntranceProgress > 99);
        SetLoopState(_enchantedWallLoop, !covering && state.EnchantedWallRunning);
    }

    private static void SetLoopState(SoundEffectInstance loop, bool shouldPlay)
    {
        if (shouldPlay && loop.State != SoundState.Playing)
        {
            loop.Play();
        }
        else if (!shouldPlay && loop.State == SoundState.Playing)
        {
            loop.Stop();
        }
    }

    private void Play(SoundEvent soundEvent, GameState state)
    {
        switch (soundEvent)
        {
            case SoundEvent.WalkEarth:
                _walkEarth.Play();
                break;
            case SoundEvent.WalkEmpty:
                _walkEmpty.Play();
                break;
            case SoundEvent.CollectJewel:
                _collectJewel.Play();
                break;
            case SoundEvent.BoulderLand:
                _boulderLand.Play();
                break;
            case SoundEvent.JewelLand:
                var jewelLandHz = SoundRecipes.JewelLandHzMin + (_random.NextDouble() * (SoundRecipes.JewelLandHzMax - SoundRecipes.JewelLandHzMin));
                RenderTriangleEffect(jewelLandHz, SoundRecipes.JewelLand).Play();
                break;
            case SoundEvent.Explosion:
                _explosion.Play();
                break;
            case SoundEvent.EntranceExplosion:
            case SoundEvent.EscapeDoorOpen:
                _crack.Play();
                break;
            case SoundEvent.TimeWarning:
                _timeWarningBySecond[Math.Clamp((int)state.CaveTimeRemaining, 0, 9)].Play();
                break;
            case SoundEvent.Uncover:
                var uncoverHz = SoundRecipes.UncoverHzMin + (_random.NextDouble() * (SoundRecipes.UncoverHzMax - SoundRecipes.UncoverHzMin));
                RenderTriangleEffect(uncoverHz, SoundRecipes.Uncover).Play();
                break;
            case SoundEvent.BonusCount:
                PlayBonusSweep();
                break;
            case SoundEvent.PushBoulder:
            case SoundEvent.Death:
                break; // kein Original-Sound dokumentiert bzw. bereits durch Explosion vertont.
        }
    }

    /// <summary>Bei jedem Betreten von LevelEndBonus aufrufen (siehe BoulderDashGame) — entspricht
    /// dem "z = $D0"-Start am Anfang von Level_End() im Original (GAME.CPP:54).</summary>
    public void ResetBonusSweep() => _bonusSweepZ = SoundRecipes.BonusSweepInitialZ;

    private void PlayBonusSweep()
    {
        const int noteCount = 15;
        var notes = new short[noteCount][];
        for (var x = noteCount; x >= 1; x--)
        {
            var hz = SoundRecipes.BonusSweepNoteHz(_bonusSweepZ, x);
            notes[noteCount - x] = SidSynth.RenderTriangle(hz, SoundRecipes.BonusSweepNote);
        }

        _bonusSweepZ--;
        BuildEffectFromSegments(notes).Play();
    }

    public void PlayMusic()
    {
        if (_music.State != SoundState.Playing)
        {
            _music.Play();
        }
    }

    public void StopMusic() => _music.Stop();

    private static SoundEffect RenderTriangleEffect(double frequencyHz, Envelope envelope) =>
        BuildEffect(SidSynth.RenderTriangle(frequencyHz, envelope));

    private SoundEffect RenderNoiseEffect(double frequencyHz, Envelope envelope) =>
        BuildEffect(SidSynth.RenderNoise(frequencyHz, envelope, _random));

    private SoundEffect BuildDroneLoop(Envelope envelope, double hzMin, double hzMax)
    {
        const int noteCount = 20;
        var notes = new short[noteCount][];
        for (var i = 0; i < noteCount; i++)
        {
            var hz = hzMin + (_random.NextDouble() * (hzMax - hzMin));
            notes[i] = SidSynth.RenderTriangle(hz, envelope);
        }

        return BuildEffectFromSegments(notes);
    }

    private static SoundEffect BuildEffectFromSegments(short[][] segments)
    {
        var total = segments.Sum(s => s.Length);
        var combined = new short[total];
        var offset = 0;
        foreach (var segment in segments)
        {
            Array.Copy(segment, 0, combined, offset, segment.Length);
            offset += segment.Length;
        }

        return BuildEffect(combined);
    }

    private static SoundEffect BuildEffect(short[] samples)
    {
        var pcm = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, pcm, 0, pcm.Length);
        return new SoundEffect(pcm, SidSynth.SampleRate, AudioChannels.Mono);
    }
}
