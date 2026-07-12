using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

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

    [Fact]
    public void WechselBoulder_durchlaeuft_genau_die_Werte_0_bis_5()
    {
        // Regressionstest: wechsel_boulder nutzt im Original ZWEI getrennte Anweisungen
        // (unbedingtes Inkrement, dann Prüfung des neuen Werts) statt des
        // Postfix-in-Bedingung-Musters der clk_*-Zähler — das ergibt Periode 6, nicht 7.
        var data = BuildCaveData();
        var cave = new BoulderDash.Core.Simulation.Cave(data);
        var state = new GameState();
        state.ResetForCave(data);
        var input = new InputState();
        input.PressRight(); // richtung!=0, damit wechsel_boulder überhaupt läuft
        var camera = new Camera();
        var clocks = new Clocks();
        var random = new Random(1);
        var tick = new GameTick(new CavePhysics(random), new ScreenCover(random), random);
        var entranceIndex = 0;

        byte[] beobachtet = new byte[8];
        for (var i = 0; i < 8; i++)
        {
            tick.Tick(cave, state, input, camera, clocks, entranceIndex);
            beobachtet[i] = state.WechselBoulder;
        }

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 0, 1, 2 }, beobachtet);
    }

    [Fact]
    public void WechselVier_durchlaeuft_genau_die_Werte_0_bis_7()
    {
        var data = BuildCaveData();
        var cave = new BoulderDash.Core.Simulation.Cave(data);
        var state = new GameState();
        state.ResetForCave(data);
        var input = new InputState();
        var camera = new Camera();
        var clocks = new Clocks();
        var random = new Random(1);
        var tick = new GameTick(new CavePhysics(random), new ScreenCover(random), random);

        byte[] beobachtet = new byte[9];
        for (var i = 0; i < 9; i++)
        {
            tick.Tick(cave, state, input, camera, clocks, 0);
            beobachtet[i] = state.WechselVier;
        }

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 0, 1 }, beobachtet);
    }

    /// <summary>Ruheanimation (BD1, BDCFF 0006): Blinzeln und Tappen werden ausschließlich zu Beginn
    /// einer 8-Frame-Sequenz neu entschieden — innerhalb einer Sequenz stehen beide fest.</summary>
    [Fact]
    public void Ruheanimation_entscheidet_sich_nur_zum_Sequenzbeginn_neu()
    {
        var (cave, state, input, camera, clocks, tick) = Setup();

        var vorherBlinzeln = state.RockfordBlinking;
        var vorherTappen = state.RockfordTapping;
        for (var i = 0; i < 400; i++)
        {
            tick.Tick(cave, state, input, camera, clocks, 0);

            if (state.RockfordBlinking != vorherBlinzeln || state.RockfordTapping != vorherTappen)
            {
                Assert.Equal(0, state.WechselVier);
            }

            vorherBlinzeln = state.RockfordBlinking;
            vorherTappen = state.RockfordTapping;
        }
    }

    /// <summary>"Can't tap or blink while moving": Bei aktiver Richtung wird gar nicht erst gewürfelt.</summary>
    [Fact]
    public void In_Bewegung_wird_weder_geblinzelt_noch_getappt()
    {
        var (cave, state, input, camera, clocks, tick) = Setup();
        input.PressRight();

        for (var i = 0; i < 400; i++)
        {
            tick.Tick(cave, state, input, camera, clocks, 0);
            Assert.False(state.RockfordBlinking);
            Assert.False(state.RockfordTapping);
        }
    }

    /// <summary>Die Wahrscheinlichkeiten der Spec: pro Sequenz blinzelt Rockford mit 1/4, und mit
    /// 1/16 schlägt das Fußtappen um. Der Zufall hat einen festen Seed, das Ergebnis ist also
    /// reproduzierbar; die Toleranzen fangen nur die Streuung der Stichprobe ab.</summary>
    [Fact]
    public void Ruheanimation_trifft_die_Wahrscheinlichkeiten_ein_Viertel_und_ein_Sechzehntel()
    {
        var (cave, state, input, camera, clocks, tick) = Setup();

        const int sequenzen = 4000;
        var blinzelSequenzen = 0;
        var tappWechsel = 0;
        var vorherTappen = state.RockfordTapping;

        for (var i = 0; i < sequenzen * 8; i++)
        {
            tick.Tick(cave, state, input, camera, clocks, 0);
            if (state.WechselVier != 0)
            {
                continue;
            }

            if (state.RockfordBlinking)
            {
                blinzelSequenzen++;
            }

            if (state.RockfordTapping != vorherTappen)
            {
                tappWechsel++;
            }

            vorherTappen = state.RockfordTapping;
        }

        Assert.InRange(blinzelSequenzen / (double)sequenzen, 0.23, 0.27);
        Assert.InRange(tappWechsel / (double)sequenzen, 0.05, 0.075);
    }

    private static (BoulderDash.Core.Simulation.Cave Cave, GameState State, InputState Input, Camera Camera, Clocks Clocks, GameTick Tick) Setup()
    {
        var data = BuildCaveData();
        var cave = new BoulderDash.Core.Simulation.Cave(data);
        var state = new GameState();
        state.ResetForCave(data);
        var random = new Random(1);
        return (cave, state, new InputState(), new Camera(), new Clocks(), new GameTick(new CavePhysics(random), new ScreenCover(random), random));
    }
}
