using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class SoundEventTests
{
    private const byte Wall = 5;

    private static CaveData BuildCaveData(int width, int height, byte[] tiles, byte enchantedWallSeconds = 0) => new()
    {
        Index = 0,
        Name = "Test",
        Description = "",
        Letter = 'A',
        IsIntermission = false,
        Width = (byte)width,
        Height = (byte)height,
        JewelQuota = 0,
        TimeSeconds = 20,
        BaseColors = [0, 1, 2, 3],
        CameraStartX = 0,
        CameraStartY = 0,
        EnchantedWallSeconds = enchantedWallSeconds,
        PointsPerJewelBeforeQuota = 10,
        PointsPerJewelAfterQuota = 20,
        GameSpeed = CaveSpeed.For(1, isIntermission: false),
        Tiles = tiles,
    };

    [Fact]
    public void Explode_reiht_ein_Explosion_Ereignis_ein()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 0, 0x42, 0, Wall,
            Wall, 0, 6, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var data = BuildCaveData(5, 4, tiles);
        var cave = new BoulderDash.Core.Simulation.Cave(data);
        var state = new GameState();
        state.ResetForCave(data);

        new CavePhysics(new BorlandRandom()).Regel(cave, state, new InputState(), new Camera(), new Clocks());

        Assert.Contains(SoundEvent.Explosion, state.SoundEvents);
    }

    [Fact]
    public void AmoebaPresent_ist_wahr_solange_Amoeba_im_Gitter_existiert()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 7, 0, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var data = BuildCaveData(5, 3, tiles);
        var cave = new BoulderDash.Core.Simulation.Cave(data);
        var state = new GameState();
        state.ResetForCave(data);

        new CavePhysics(new BorlandRandom()).Regel(cave, state, new InputState(), new Camera(), new Clocks());

        Assert.True(state.AmoebaPresent);
    }

    [Fact]
    public void AmoebaPresent_ist_falsch_ohne_Amoeba_im_Gitter()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 0, 0, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var data = BuildCaveData(5, 3, tiles);
        var cave = new BoulderDash.Core.Simulation.Cave(data);
        var state = new GameState();
        state.ResetForCave(data);

        new CavePhysics(new BorlandRandom()).Regel(cave, state, new InputState(), new Camera(), new Clocks());

        Assert.False(state.AmoebaPresent);
    }

    [Fact]
    public void TimeWarning_wird_erst_ab_9_Sekunden_Restzeit_eingereiht()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 10, 0, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var data = BuildCaveData(5, 3, tiles);
        var cave = new BoulderDash.Core.Simulation.Cave(data);
        var state = new GameState();
        state.ResetForCave(data);
        state.CaveTimeRemaining = 11; // -> nach zwei Sekunden-Countdowns bei 9 (Warnung), dann bei 10 (keine)
        var entranceIndex = cave.FindFirstIndexOf(Element.Entrance);
        var random = new BorlandRandom();
        var tick = new GameTick(new CavePhysics(random), new ScreenCover(random));
        var input = new InputState();
        var camera = new Camera();
        var clocks = new Clocks();

        // EntranceProgress überschreitet 99 um Tick~100; Clk18 (Periode 22) erreicht danach bei
        // Tick 110 den ersten Countdown (11->10) und bei Tick 132 den zweiten (10->9, Warnung).
        for (var i = 0; i < 140; i++)
        {
            tick.Tick(cave, state, input, camera, clocks, entranceIndex);
        }

        Assert.Contains(SoundEvent.TimeWarning, state.SoundEvents);
        Assert.Equal(9, state.CaveTimeRemaining);
    }

    [Fact]
    public void Uncover_wird_nur_waehrend_der_Aufdeck_Phase_eingereiht()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 10, 0, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var data = BuildCaveData(5, 3, tiles);
        var cave = new BoulderDash.Core.Simulation.Cave(data);
        var state = new GameState();
        state.ResetForCave(data);
        var entranceIndex = cave.FindFirstIndexOf(Element.Entrance);
        var random = new BorlandRandom();
        var cover = new ScreenCover(random);
        cover.BeginUncover(data.Width, data.Height);
        var tick = new GameTick(new CavePhysics(random), cover);
        var input = new InputState();
        var camera = new Camera();
        var clocks = new Clocks();

        tick.Tick(cave, state, input, camera, clocks, entranceIndex);
        Assert.Contains(SoundEvent.Uncover, state.SoundEvents);
        Assert.True(state.ScreenCoverActive);

        // Restliche Aufdeck-Runden abarbeiten (eine pro Tick)...
        for (var i = 1; i < ScreenCover.Iterations; i++)
        {
            tick.Tick(cave, state, input, camera, clocks, entranceIndex);
        }

        Assert.False(cover.IsActive);
        Assert.False(state.ScreenCoverActive);
        state.SoundEvents.Clear();

        // ...dann darf kein neues Uncover-Ereignis mehr eingereiht werden.
        tick.Tick(cave, state, input, camera, clocks, entranceIndex);

        Assert.DoesNotContain(SoundEvent.Uncover, state.SoundEvents);
    }
}
