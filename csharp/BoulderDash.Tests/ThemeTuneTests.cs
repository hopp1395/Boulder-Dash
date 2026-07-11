using BoulderDash.Core.Audio;

namespace BoulderDash.Tests;

public class ThemeTuneTests
{
    [Theory]
    [InlineData(0x16, 89.3)]
    [InlineData(0x2E, 357.0)]
    public void HzForIndex_liefert_die_in_der_Dokumentation_angegebenen_Frequenzen(int index, double expectedHz)
    {
        var hz = ThemeTune.HzForIndex(index);

        Assert.Equal(expectedHz, hz, precision: 1);
    }

    [Theory]
    [InlineData(0x09)]
    [InlineData(0x3B)]
    public void HzForIndex_wirft_ausserhalb_des_Tabellenbereichs(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThemeTune.HzForIndex(index));
    }

    [Fact]
    public void Render_liefert_die_fuer_128_Noten_erwartete_Samplezahl()
    {
        const double noteDuration = 0.1;

        var samples = ThemeTune.Render(noteDuration);

        Assert.Equal(0, samples.Length % 128);
        var samplesPerNote = samples.Length / 128;
        var expectedPerNote = (int)(noteDuration * SidSynth.SampleRate);
        Assert.InRange(samplesPerNote, expectedPerNote - 5, expectedPerNote + 5);
    }

    [Fact]
    public void Render_beginnt_und_endet_nahe_Null_fuer_nahtloses_Looping()
    {
        var samples = ThemeTune.Render(0.1);

        // Attack/Release-Rampen sorgen für nahezu Null an den Rändern, nicht exakt Null
        // (Rundung der Envelope-Samplezahl) — großzügige Toleranz reicht zum Knacks-Ausschluss.
        Assert.InRange(samples[0], (short)-2000, (short)2000);
        Assert.InRange(samples[^1], (short)-2000, (short)2000);
    }
}
