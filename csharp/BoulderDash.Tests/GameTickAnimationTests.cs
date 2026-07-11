using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class GameTickAnimationTests
{
    private const byte Wall = 5;

    private static CaveData BuildCaveData() => new()
    {
        Index = 0,
        Width = 5,
        Height = 3,
        JewelQuota = 0,
        TimeSeconds = 99,
        BaseColors = [0, 1, 2, 3],
        CameraStartX = 0,
        CameraStartY = 0,
        EnchantedWallSeconds = 0,
        PointsPerJewelBeforeQuota = 10,
        PointsPerJewelAfterQuota = 20,
        GameSpeed = 1,
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
        var cave = new Cave(data);
        var state = new GameState();
        state.ResetForCave(data);
        var input = new InputState();
        input.PressRight(); // richtung!=0, damit wechsel_boulder überhaupt läuft
        var camera = new Camera();
        var clocks = new Clocks();
        var random = new BorlandRandom();
        var tick = new GameTick(new CavePhysics(random), new Dissolve(random));
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
        var cave = new Cave(data);
        var state = new GameState();
        state.ResetForCave(data);
        var input = new InputState();
        var camera = new Camera();
        var clocks = new Clocks();
        var random = new BorlandRandom();
        var tick = new GameTick(new CavePhysics(random), new Dissolve(random));

        byte[] beobachtet = new byte[9];
        for (var i = 0; i < 9; i++)
        {
            tick.Tick(cave, state, input, camera, clocks, 0);
            beobachtet[i] = state.WechselVier;
        }

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 0, 1 }, beobachtet);
    }
}
