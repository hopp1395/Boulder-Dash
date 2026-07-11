using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class CaveSpeedTests
{
    /// <summary>Das Tempo steht als GameSpeed (ms pro Cave-Scan) in der Cave-Datei — geladen wird,
    /// was dort steht. Dieser Test prüft die ausgelieferten 100 Dateien gegen die BD1-Herleitung
    /// (CaveSpeed.For), damit die Daten nicht unbemerkt vom Original abdriften.</summary>
    [Fact]
    public void Alle_Cave_Dateien_tragen_das_BD1_Tempo_ihres_Levels()
    {
        var caves = new CaveTextRepository(Path.Combine(TestPaths.GameAssets, "Caves"));
        Assert.True(caves.Get("cave-Q-1").IsIntermission, "Cave Q muss eine Intermission sein.");

        for (var letter = 'A'; letter <= 'T'; letter++)
        {
            for (var level = 1; level <= 5; level++)
            {
                var cave = caves.Get($"cave-{letter}-{level}");
                var erwartet = CaveSpeed.For(level, cave.IsIntermission);

                Assert.True(
                    cave.GameSpeed == erwartet,
                    $"cave-{letter}-{level}: GameSpeed {cave.GameSpeed.SecondsPerScan * 1000:F0} ms, erwartet {erwartet.SecondsPerScan * 1000:F0} ms.");
            }
        }
    }

    [Fact]
    public void Grad_1_behaelt_das_bisherige_Tempo_150ms_pro_Scan()
    {
        // Anker: der Port scannte bisher alle 3 Ticks à 50 ms — das ist exakt BD1 Grad 1.
        var speed = CaveSpeed.For(1, isIntermission: false);

        Assert.Equal(0.150, speed.SecondsPerScan, 3);
        Assert.Equal(0.050, speed.SecondsPerTick, 3);
        Assert.Equal(Clocks.DefaultGameSecondTicks, speed.GameSecondTicks);
    }

    [Fact]
    public void Tempo_steigt_streng_monoton_mit_dem_Schwierigkeitsgrad()
    {
        foreach (var intermission in new[] { false, true })
        {
            for (var level = 1; level < 5; level++)
            {
                Assert.True(
                    CaveSpeed.For(level + 1, intermission).SecondsPerScan < CaveSpeed.For(level, intermission).SecondsPerScan,
                    $"Grad {level + 1} muss schneller scannen als Grad {level} (Intermission={intermission}).");
            }
        }
    }

    [Fact]
    public void Intermissions_laufen_schneller_als_regulaere_Caves()
    {
        // BD1: Basis 60 ms statt 88 ms pro Scan.
        for (var level = 1; level <= 5; level++)
        {
            Assert.True(
                CaveSpeed.For(level, isIntermission: true).SecondsPerScan < CaveSpeed.For(level, isIntermission: false).SecondsPerScan,
                $"Intermission muss bei Grad {level} schneller sein als eine reguläre Cave.");
        }
    }

    [Fact]
    public void Spielsekunde_bleibt_bei_jedem_Tempo_gleich_lang()
    {
        // Der Kern der Entkopplung: in BD1 zählt der Timer IRQ-getrieben und damit tempo-unabhängig.
        // Die Clk18-Periode muss das Tempo also genau ausgleichen (Rest: Rundung auf ganze Ticks).
        foreach (var intermission in new[] { false, true })
        {
            for (var level = 1; level <= 5; level++)
            {
                var speed = CaveSpeed.For(level, intermission);
                var sekunde = speed.GameSecondTicks * speed.SecondsPerTick;

                Assert.True(
                    Math.Abs(sekunde - CaveSpeed.GameSecondSeconds) <= 0.03 * CaveSpeed.GameSecondSeconds,
                    $"Spielsekunde bei Grad {level} (Intermission={intermission}) ist {sekunde:F3}s statt {CaveSpeed.GameSecondSeconds}s.");
            }
        }
    }

    [Fact]
    public void Spielsekunde_ist_laenger_als_eine_echte_Sekunde()
    {
        // BD1: ~64 Ticks pro Spielsekunde statt 60 ("a game second is more than 60 ticks").
        Assert.True(CaveSpeed.GameSecondSeconds > 1.0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Ungueltiger_Schwierigkeitsgrad_wird_abgelehnt(int level)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CaveSpeed.For(level, isIntermission: false));
    }
}
