using BoulderDash.Core.Audio;

namespace BoulderDash.Tests;

public class SidSynthTests
{
    private static readonly Envelope SimpleEnvelope = new()
    {
        AttackSeconds = 0.01, DecaySeconds = 0.01, SustainLevel = 0.5, HoldSeconds = 0.02, ReleaseSeconds = 0.02,
    };

    [Fact]
    public void RenderTriangle_liefert_die_aus_der_Huellkurve_erwartete_Samplezahl()
    {
        var samples = SidSynth.RenderTriangle(440.0, SimpleEnvelope);

        var expected = SimpleEnvelope.TotalSamples(SidSynth.SampleRate);
        Assert.Equal(expected, samples.Length);
    }

    [Fact]
    public void RenderTriangle_hat_die_zur_Frequenz_passende_Anzahl_Nulldurchgaenge()
    {
        // Bei fester Frequenz f über t Sekunden sind ~2*f*t Nulldurchgänge zu erwarten.
        var envelope = new Envelope { AttackSeconds = 0, DecaySeconds = 0, SustainLevel = 1.0, HoldSeconds = 0.1, ReleaseSeconds = 0 };
        var samples = SidSynth.RenderTriangle(1000.0, envelope);

        var crossings = 0;
        for (var i = 1; i < samples.Length; i++)
        {
            if (Math.Sign(samples[i - 1]) != Math.Sign(samples[i]) && samples[i - 1] != 0)
            {
                crossings++;
            }
        }

        var expected = 2 * 1000.0 * 0.1; // 200
        Assert.InRange(crossings, expected - 5, expected + 5);
    }

    [Fact]
    public void RenderNoise_ist_nicht_konstant()
    {
        var samples = SidSynth.RenderNoise(1000.0, SimpleEnvelope, new Random(1));

        Assert.True(samples.Distinct().Count() > 1);
    }

    [Fact]
    public void Huellkurve_mit_SustainLevel_0_klingt_am_Ende_der_Decay_Phase_auf_Null_ab()
    {
        var envelope = new Envelope { AttackSeconds = 0.01, DecaySeconds = 0.01, SustainLevel = 0.0, ReleaseSeconds = 0.006 };

        var atEndOfDecay = envelope.AmplitudeAt(
            (int)((envelope.AttackSeconds + envelope.DecaySeconds) * SidSynth.SampleRate), SidSynth.SampleRate);

        Assert.Equal(0.0, atEndOfDecay, precision: 3);
    }
}
