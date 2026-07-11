using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class CavePhysicsTests
{
    private const byte Wall = 5; // TitaniumWall, als Rand für alle Testgitter

    private static CaveData BuildCaveData(int width, int height, byte[] tiles, byte jewelQuota = 0,
        byte pointsBefore = 10, byte pointsAfter = 20, byte enchantedWallSeconds = 0) => new()
    {
        Index = 0,
        Name = "Test",
        Description = "",
        Letter = 'A',
        IsIntermission = false,
        Width = (byte)width,
        Height = (byte)height,
        JewelQuota = jewelQuota,
        TimeSeconds = 99,
        BaseColors = [0, 1, 2, 3],
        CameraStartX = 0,
        CameraStartY = 0,
        EnchantedWallSeconds = enchantedWallSeconds,
        PointsPerJewelBeforeQuota = pointsBefore,
        PointsPerJewelAfterQuota = pointsAfter,
        GameSpeed = 1,
        Tiles = tiles,
    };

    private static (BoulderDash.Core.Simulation.Cave Cave, GameState State) Setup(CaveData data)
    {
        var cave = new BoulderDash.Core.Simulation.Cave(data);
        var state = new GameState();
        state.ResetForCave(data);
        return (cave, state);
    }

    private static CavePhysics NewPhysics() => new(new BorlandRandom());

    [Fact]
    public void Boulder_faellt_in_leere_Zelle_darunter()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 0, 2, 0, Wall,
            Wall, 0, 0, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(5, 4, tiles));

        NewPhysics().Regel(cave, state, new InputState(), new Camera(), new Clocks());

        Assert.Equal(Element.Empty, cave.GetElement(2, 1));
        Assert.Equal(Element.Boulder, cave.GetElement(2, 2));
    }

    [Fact]
    public void Boulder_rollt_zur_Seite_wenn_Platz_frei_ist()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 0, 2, 0, Wall,
            Wall, 2, 4, 0, Wall, // darunter: Stein(links), Mauer(unter Boulder), leer(rechts)
            Wall, Wall, Wall, Wall, Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(5, 4, tiles));

        NewPhysics().Regel(cave, state, new InputState(), new Camera(), new Clocks());

        // Links blockiert (Stein bei (1,2), kein leerer Diagonalplatz), also Rollen nach rechts.
        Assert.Equal(Element.Empty, cave.GetElement(2, 1));
        Assert.Equal(Element.Boulder, cave.GetElement(3, 1));
    }

    [Fact]
    public void Rockford_graebt_Erde_und_bewegt_sich()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 6, 1, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(5, 3, tiles));
        var input = new InputState();
        input.PressRight();

        NewPhysics().Regel(cave, state, input, new Camera(), new Clocks());

        Assert.Equal(Element.Empty, cave.GetElement(1, 1));
        Assert.Equal(Element.Rockford, cave.GetElement(2, 1));
    }

    [Fact]
    public void Rockford_sammelt_Diamant_und_erhoeht_Punktestand()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 6, 3, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(5, 3, tiles, jewelQuota: 5, pointsBefore: 10, pointsAfter: 20));
        var input = new InputState();
        input.PressRight();

        NewPhysics().Regel(cave, state, input, new Camera(), new Clocks());

        Assert.Equal(1, state.JewelsCollected);
        Assert.Equal(10, state.Score);
        Assert.Equal(Element.Rockford, cave.GetElement(2, 1));
    }

    [Fact]
    public void Letzter_Diamant_zur_Quote_wird_bereits_mit_dem_hoeheren_Punktwert_gewertet()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 6, 3, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(5, 3, tiles, jewelQuota: 1, pointsBefore: 10, pointsAfter: 20));
        var input = new InputState();
        input.PressRight();

        NewPhysics().Regel(cave, state, input, new Camera(), new Clocks());

        Assert.Equal(1, state.JewelsCollected);
        Assert.Equal(20, state.Score); // Quote mit diesem Diamanten erreicht -> sofort neuer Punktwert
    }

    [Fact]
    public void Greifen_ohne_Bewegen_laesst_Rockford_an_Ort_und_Stelle()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 6, 1, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(5, 3, tiles));
        var input = new InputState();
        input.PressRight();
        input.PressGrab();

        NewPhysics().Regel(cave, state, input, new Camera(), new Clocks());

        Assert.Equal(Element.Rockford, cave.GetElement(1, 1)); // bleibt stehen
        Assert.Equal(Element.Empty, cave.GetElement(2, 1)); // Erde trotzdem entfernt
    }

    [Fact]
    public void Rockford_schiebt_Stein_horizontal_wenn_dahinter_Platz_ist()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall, Wall,
            Wall, 6, 2, 0, 0, Wall,
            Wall, Wall, Wall, Wall, Wall, Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(6, 3, tiles));
        var input = new InputState();
        input.PressRight();
        var clocks = new Clocks(); // Clk4 startet bei 0 -> Schub-Fenster offen

        NewPhysics().Regel(cave, state, input, new Camera(), clocks);

        Assert.Equal(Element.Empty, cave.GetElement(1, 1));
        Assert.Equal(Element.Rockford, cave.GetElement(2, 1));
        Assert.Equal(Element.Boulder, cave.GetElement(3, 1));
    }

    [Fact]
    public void Stein_schiebt_nicht_wenn_Schubfenster_clk4_nicht_null_ist()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall, Wall,
            Wall, 6, 2, 0, 0, Wall,
            Wall, Wall, Wall, Wall, Wall, Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(6, 3, tiles));
        var input = new InputState();
        input.PressRight();
        var clocks = new Clocks();
        clocks.Tick(); // Clk1=1,Clk4=1,Clk18=1 -> Schub-Fenster (Clk4==0) geschlossen

        NewPhysics().Regel(cave, state, input, new Camera(), clocks);

        Assert.Equal(Element.Rockford, cave.GetElement(1, 1)); // unverändert, kein Schub
        Assert.Equal(Element.Boulder, cave.GetElement(2, 1));
    }

    [Fact]
    public void Fallender_Stein_toetet_Rockford_per_Explosion()
    {
        // Stein mit Fall-Momentum (Bit 0x40) direkt über Rockford.
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 0, 0x42, 0, Wall,
            Wall, 0, 6, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(5, 4, tiles));

        NewPhysics().Regel(cave, state, new InputState(), new Camera(), new Clocks());

        Assert.Equal(Element.Explosion, cave.GetElement(2, 2));
        Assert.Equal(1, state.WechselExplo);
    }

    [Fact]
    public void Zaubermauer_wandelt_fallenden_Stein_zwei_Zeilen_tiefer_in_Diamant()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 0, 0x42, 0, Wall, // Stein mit Momentum
            Wall, 0, 13, 0, Wall, // Zaubermauer darunter
            Wall, 0, 0, 0, Wall, // Zielzeile für die Umwandlung
            Wall, Wall, Wall, Wall, Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(5, 5, tiles, enchantedWallSeconds: 10));

        NewPhysics().Regel(cave, state, new InputState(), new Camera(), new Clocks());

        Assert.True(state.EnchantedWallRunning);
        Assert.Equal(Element.Empty, cave.GetElement(2, 1));
        Assert.Equal(Element.Jewel, cave.GetElement(2, 3));
    }

    [Fact]
    public void Rockford_bewegt_sich_nicht_wenn_Kamera_Aufwaertsscroll_ausloest()
    {
        // Original-Dangling-Else (BOULDER.CPP:896-898): löst die Rockford-Zeile den
        // Kamera-Aufwärtsscroll aus (camera.Y+1==row && camera.Y>0), bleibt die
        // Bewegungsverarbeitung diesen Tick komplett aus — auch wenn eine Bewegungstaste liegt.
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 0, 0, 0, Wall,
            Wall, 6, 1, 0, Wall, // Rockford in Zeile 2
            Wall, Wall, Wall, Wall, Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(5, 4, tiles));
        var input = new InputState();
        input.PressRight();
        var camera = new Camera();
        camera.ResetTo(0, 1); // camera.Y=1>0, Rockford-Zeile=2 -> camera.Y+1==row trifft zu

        NewPhysics().Regel(cave, state, input, camera, new Clocks());

        Assert.Equal(Element.Rockford, cave.GetElement(1, 2)); // keine Bewegung ausgeführt
        Assert.Equal(Element.Earth, cave.GetElement(2, 2)); // Erde unverändert
        Assert.Equal((sbyte)-5, camera.Rely); // Scroll-Ziel wurde trotzdem gesetzt
    }
}
