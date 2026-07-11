using BoulderDash.Core.Simulation;
using Microsoft.Xna.Framework.Audio;
using NVorbis;

namespace BoulderDash.Game.Audio;

/// <summary>
/// Lädt die OGG-Dateien aus sound/ und spielt SoundEvent-Ereignisse aus GameState.SoundEvents ab.
/// Der DOS-Klon selbst hat faktisch keinen Sound (SOUND.BIN wird nie geladen, siehe BOULDER.CPP);
/// der Port nutzt stattdessen diese moderne Vertonung. Nur ein Teil der SoundEvent-Werte hat
/// aktuell eine Datei — die übrigen bleiben bewusst stumm, bis Sounds ergänzt werden.
/// </summary>
public sealed class AudioPlayer
{
    private readonly Dictionary<SoundEvent, SoundEffect> _effects = new();
    private readonly SoundEffectInstance? _music;

    public AudioPlayer(string soundFolder)
    {
        TryLoad(SoundEvent.WalkEarth, Path.Combine(soundFolder, "walk_earth.ogg"));
        TryLoad(SoundEvent.WalkEmpty, Path.Combine(soundFolder, "walk_empty.ogg"));
        TryLoad(SoundEvent.CollectJewel, Path.Combine(soundFolder, "collect_diamond.ogg"));
        TryLoad(SoundEvent.BoulderLand, Path.Combine(soundFolder, "boulder.ogg"));

        var musicPath = Path.Combine(soundFolder, "bdmusic.ogg");
        if (File.Exists(musicPath))
        {
            _music = LoadOgg(musicPath).CreateInstance();
            _music.IsLooped = true;
        }
    }

    private void TryLoad(SoundEvent soundEvent, string path)
    {
        if (File.Exists(path))
        {
            _effects[soundEvent] = LoadOgg(path);
        }
    }

    private static SoundEffect LoadOgg(string path)
    {
        using var reader = new VorbisReader(path);
        var totalFloats = (int)(reader.TotalSamples * reader.Channels);
        var floatBuffer = new float[totalFloats];
        reader.ReadSamples(floatBuffer, 0, totalFloats);

        var pcm = new byte[totalFloats * 2];
        for (var i = 0; i < totalFloats; i++)
        {
            var sample = Math.Clamp(floatBuffer[i], -1f, 1f);
            var value = (short)(sample * short.MaxValue);
            pcm[i * 2] = (byte)(value & 0xFF);
            pcm[(i * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        var channels = reader.Channels == 2 ? AudioChannels.Stereo : AudioChannels.Mono;
        return new SoundEffect(pcm, reader.SampleRate, channels);
    }

    /// <summary>Leert die Ereigniswarteschlange eines Ticks und spielt bekannte Sounds ab.</summary>
    public void DrainAndPlay(Queue<SoundEvent> events)
    {
        while (events.Count > 0)
        {
            var soundEvent = events.Dequeue();
            if (_effects.TryGetValue(soundEvent, out var effect))
            {
                effect.Play();
            }
        }
    }

    public void PlayMusic()
    {
        if (_music is not null && _music.State != SoundState.Playing)
        {
            _music.Play();
        }
    }

    public void StopMusic()
    {
        _music?.Stop();
    }
}
