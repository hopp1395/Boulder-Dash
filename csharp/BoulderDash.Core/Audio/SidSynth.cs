namespace BoulderDash.Core.Audio;

/// <summary>
/// Einfacher Software-Synthesizer für die in der Boulder-Dash-Sound-Dokumentation beschriebenen
/// SID-Wellenformen (Dreieck, weißes Rauschen). Kein bitgenauer SID-Emulator, sondern eine
/// hörbar ähnliche, deutlich vereinfachte Nachbildung: Dreieck als reine Phasenrampe, Rauschen
/// als Sample-and-Hold mit frequenzabhängiger Haltezeit (der SID aktualisiert sein internes
/// Rauschregister ebenfalls nicht mit der Abtastrate, sondern mit einer von der Frequenz
/// abgeleiteten, deutlich niedrigeren Rate).
/// </summary>
public static class SidSynth
{
    public const int SampleRate = 44100;

    public static short[] RenderTriangle(double frequencyHz, Envelope envelope)
    {
        var totalSamples = envelope.TotalSamples(SampleRate);
        var samples = new short[totalSamples];
        var phase = 0.0;
        var phaseStep = frequencyHz / SampleRate;

        for (var i = 0; i < totalSamples; i++)
        {
            phase += phaseStep;
            phase -= Math.Floor(phase);
            var triangle = (phase < 0.5 ? phase : 1.0 - phase) * 4.0 - 1.0; // -1..1
            samples[i] = (short)(triangle * envelope.AmplitudeAt(i, SampleRate) * short.MaxValue);
        }

        return samples;
    }

    public static short[] RenderNoise(double frequencyHz, Envelope envelope, Random random)
    {
        var totalSamples = envelope.TotalSamples(SampleRate);
        var samples = new short[totalSamples];
        var samplesPerHold = Math.Max(1, (int)(SampleRate / Math.Max(1.0, frequencyHz) / 2.0));
        short current = 0;

        for (var i = 0; i < totalSamples; i++)
        {
            if (i % samplesPerHold == 0)
            {
                current = (short)random.Next(short.MinValue, short.MaxValue);
            }

            samples[i] = (short)(current * envelope.AmplitudeAt(i, SampleRate));
        }

        return samples;
    }
}
