using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class GameTickTests
{
    private const byte Wall = 5;

    private static CaveData BuildCaveData(int width, int height, byte[] tiles) => new()
    {
        Index = 0,
        Width = (byte)width,
        Height = (byte)height,
        JewelQuota = 0,
        TimeSeconds = 99,
        BaseColors = [0, 1, 2, 3],
        CameraStartX = 0,
        CameraStartY = 0,
        EnchantedWallSeconds = 0,
        PointsPerJewelBeforeQuota = 10,
        PointsPerJewelAfterQuota = 20,
        GameSpeed = 1,
        Tiles = tiles,
    };

    [Fact]
    public void Eingang_erzeugt_Explosion_bei_92_und_Rockford_bei_99()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 10, 0, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var data = BuildCaveData(5, 3, tiles);
        var cave = new Cave(data);
        var state = new GameState();
        state.ResetForCave(data);
        var entranceIndex = cave.FindFirstIndexOf(Element.Entrance);
        var input = new InputState();
        var camera = new Camera();
        var clocks = new Clocks();
        var random = new BorlandRandom();
        var physics = new CavePhysics(random);
        var dissolve = new Dissolve(random);
        var tick = new GameTick(physics, dissolve);

        // Eintretend mit EntranceProgress==92 löst die Explosion aus -> 93 Ticks nötig
        // (Tick 1 bringt EntranceProgress von 0 auf 1, ..., Tick 93 sieht beim Eintritt 92).
        for (var i = 0; i < 93; i++)
        {
            tick.Tick(cave, state, input, camera, clocks, entranceIndex);
        }

        Assert.Equal(93, state.EntranceProgress);
        Assert.Equal(Element.Explosion, cave.GetElement(entranceIndex % 5, entranceIndex / 5));

        // Analog: EntranceProgress==99 beim Eintritt löst den Rockford-Spawn aus -> 100 Ticks gesamt.
        for (var i = 93; i < 100; i++)
        {
            tick.Tick(cave, state, input, camera, clocks, entranceIndex);
        }

        Assert.Equal(100, state.EntranceProgress);
        Assert.Equal(Element.Rockford, cave.GetElement(entranceIndex % 5, entranceIndex / 5));
    }
}
