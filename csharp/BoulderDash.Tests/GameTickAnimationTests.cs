using BoulderDash.Core.Data;
using BoulderDash.Core.Objects;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

/// <summary>
/// Die Animationszähler. Sie standen früher als wechsel_vier/wechsel_boulder in GameState — EIN Zähler
/// für alle Objekte. Inzwischen führt sie, wer sie braucht: den gemeinsamen Achtertakt die Cave
/// (Cave.AnimationPhase), den Laufzyklus und die Ruheanimation Rockford selbst (RockfordObject).
/// Getaktet werden sie nach wie vor vom GameTick.
/// </summary>
public class GameTickAnimationTests
{
    private const byte Wall = 5;

    private static CaveData BuildCaveData() => new()
    {
        Index = 0,
        Name = "Test",
        Description = "",
        Letter = 'A',
        IsIntermission = false,
        Width = 5,
        Height = 3,
        JewelQuota = 0,
        TimeSeconds = 99,
        Colors = [new(0x20, 0x20, 0x20), new(0xFF, 0xFF, 0xFF), new(0xBA, 0x20, 0x20), new(0x71, 0xFF, 0xFF)],
        EnchantedWallSeconds = 0,
        AmoebaSlowGrowthSeconds = 0,
        PointsPerJewelBeforeQuota = 10,
        PointsPerJewelAfterQuota = 20,
        GameSpeed = CaveSpeed.For(1, isIntermission: false),
        Tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 6, 0, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ],
    };

    /// <summary>Cave mit Rockford im Gitter und fertigem Eingang: EntranceProgress=101 hält den
    /// Eingangsaufbau still (GameTick baut nur unterhalb von 101), sonst spränge nach 99 Ticks ein
    /// ZWEITER Rockford aus der Eingangskachel und FindRockford() lieferte über die Testdauer hinweg
    /// verschiedene Objekte.</summary>
    private static (Cave Cave, Clocks Clocks, GameTick Tick) Setup()
    {
        var random = new Random(1);
        var cave = TestWorld.NewCave(BuildCaveData(), random);
        cave.State.EntranceProgress = 101;
        return (cave, new Clocks(), TestWorld.NewTick(random));
    }

    private static RockfordObject Rockford(Cave cave) => cave.FindRockford()!;

    [Fact]
    public void WalkPhase_durchlaeuft_genau_die_Werte_0_bis_5()
    {
        // Regressionstest: wechsel_boulder nutzt im Original ZWEI getrennte Anweisungen
        // (unbedingtes Inkrement, dann Prüfung des neuen Werts) statt des
        // Postfix-in-Bedingung-Musters der clk_*-Zähler — das ergibt Periode 6, nicht 7.
        var (cave, clocks, tick) = Setup();
        cave.Input.PressRight(); // richtung!=0, damit der Laufzyklus überhaupt läuft

        var beobachtet = new byte[8];
        for (var i = 0; i < 8; i++)
        {
            tick.Tick(cave, clocks, entranceIndex: 0);
            beobachtet[i] = Rockford(cave).WalkPhase;
        }

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 0, 1, 2 }, beobachtet);
    }

    [Fact]
    public void AnimationPhase_durchlaeuft_genau_die_Werte_0_bis_7()
    {
        var (cave, clocks, tick) = Setup();

        var beobachtet = new byte[9];
        for (var i = 0; i < 9; i++)
        {
            tick.Tick(cave, clocks, entranceIndex: 0);
            beobachtet[i] = cave.AnimationPhase;
        }

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 0, 1 }, beobachtet);
    }

    /// <summary>Ruheanimation (BD1, BDCFF 0006): Blinzeln und Tappen werden ausschließlich zu Beginn
    /// einer 8-Frame-Sequenz neu entschieden — innerhalb einer Sequenz stehen beide fest.</summary>
    [Fact]
    public void Ruheanimation_entscheidet_sich_nur_zum_Sequenzbeginn_neu()
    {
        var (cave, clocks, tick) = Setup();

        var vorherBlinzeln = Rockford(cave).Blinking;
        var vorherTappen = Rockford(cave).Tapping;
        for (var i = 0; i < 400; i++)
        {
            tick.Tick(cave, clocks, entranceIndex: 0);
            var rockford = Rockford(cave);

            if (rockford.Blinking != vorherBlinzeln || rockford.Tapping != vorherTappen)
            {
                Assert.Equal(0, cave.AnimationPhase);
            }

            vorherBlinzeln = rockford.Blinking;
            vorherTappen = rockford.Tapping;
        }
    }

    /// <summary>"Can't tap or blink while moving": Bei aktiver Richtung wird gar nicht erst gewürfelt.</summary>
    [Fact]
    public void In_Bewegung_wird_weder_geblinzelt_noch_getappt()
    {
        var (cave, clocks, tick) = Setup();
        cave.Input.PressRight();

        for (var i = 0; i < 400; i++)
        {
            tick.Tick(cave, clocks, entranceIndex: 0);
            var rockford = Rockford(cave);

            Assert.False(rockford.Blinking);
            Assert.False(rockford.Tapping);
        }
    }

    /// <summary>Die Wahrscheinlichkeiten der Spec: pro Sequenz blinzelt Rockford mit 1/4, und mit
    /// 1/16 schlägt das Fußtappen um. Der Zufall hat einen festen Seed, das Ergebnis ist also
    /// reproduzierbar; die Toleranzen fangen nur die Streuung der Stichprobe ab.</summary>
    [Fact]
    public void Ruheanimation_trifft_die_Wahrscheinlichkeiten_ein_Viertel_und_ein_Sechzehntel()
    {
        var (cave, clocks, tick) = Setup();

        const int sequenzen = 4000;
        var blinzelSequenzen = 0;
        var tappWechsel = 0;
        var vorherTappen = Rockford(cave).Tapping;

        for (var i = 0; i < sequenzen * 8; i++)
        {
            tick.Tick(cave, clocks, entranceIndex: 0);
            if (cave.AnimationPhase != 0)
            {
                continue;
            }

            var rockford = Rockford(cave);
            if (rockford.Blinking)
            {
                blinzelSequenzen++;
            }

            if (rockford.Tapping != vorherTappen)
            {
                tappWechsel++;
            }

            vorherTappen = rockford.Tapping;
        }

        Assert.InRange(blinzelSequenzen / (double)sequenzen, 0.23, 0.27);
        Assert.InRange(tappWechsel / (double)sequenzen, 0.05, 0.075);
    }
}
