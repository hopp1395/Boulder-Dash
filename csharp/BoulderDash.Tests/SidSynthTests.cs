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

    /// <summary>Der Sweep zieht die Frequenz über seine Stufen hoch — messbar daran, dass die zweite
    /// Hälfte des Signals deutlich mehr Nulldurchgänge hat als die erste.</summary>
    [Fact]
    public void RenderTriangleSweep_steigt_in_der_Tonhoehe_ueber_die_Stufen()
    {
        var envelope = new Envelope { AttackSeconds = 0, DecaySeconds = 0, SustainLevel = 1.0, HoldSeconds = 0.1, ReleaseSeconds = 0 };
        double[] frequencies = [500, 1000, 1500, 2000, 2500, 3000, 3500, 4000, 4500, 5000];

        var samples = SidSynth.RenderTriangleSweep(frequencies, 0.01, envelope);

        Assert.Equal(envelope.TotalSamples(SidSynth.SampleRate), samples.Length);
        var half = samples.Length / 2;
        Assert.True(ZeroCrossings(samples[..half]) * 2 < ZeroCrossings(samples[half..]));
    }

    private static int ZeroCrossings(short[] samples)
    {
        var crossings = 0;
        for (var i = 1; i < samples.Length; i++)
        {
            if (Math.Sign(samples[i - 1]) != Math.Sign(samples[i]) && samples[i - 1] != 0)
            {
                crossings++;
            }
        }

        return crossings;
    }

    /// <summary>Byte-Arithmetik des Bonus-Sweeps (Peter Broadribb, "Bonus points sound"): z zählt als
    /// Byte herunter und läuft bei mehr als 208 Bonussekunden — im BD1-Bonusüberlauf mit 255 also
    /// immer — unten heraus; die Tonhöhe springt dabei von ganz tief zurück nach ganz hoch. Ohne
    /// Byte-Rechnung ergäben sich negative Frequenzen.</summary>
    [Fact]
    public void BonusSweep_z_laeuft_als_Byte_ueber_und_bleibt_dabei_hoerbar()
    {
        var z = SoundRecipes.BonusSweepInitialZ;

        for (var second = 0; second < 255; second++)
        {
            z = SoundRecipes.BonusSweepNextZ(z);
            Assert.InRange(z, 0, 255);

            for (var x = SoundRecipes.BonusSweepNoteCount; x >= 1; x--)
            {
                Assert.InRange(SoundRecipes.BonusSweepNoteHz(z, x), 0.0, 4000.0);
            }
        }

        // 255 Schritte ab $D0: 208-255 = -47 -> als Byte 209.
        Assert.Equal(209, z);
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
