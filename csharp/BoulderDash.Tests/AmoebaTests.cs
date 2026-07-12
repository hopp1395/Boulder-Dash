using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

/// <summary>
/// Die Amoeba folgt BD1 (BDCFF-Objektspezifikation 000A), nicht dem DOS-Original: jede Zelle würfelt
/// pro Cave-Scan einzeln ihr Wachstum aus (3 %, nach Ablauf der Amoeba-Zeit 25 %) und wächst in eine
/// zufällige der vier Richtungen; ab 200 Zellen wird sie zu Boulders, eingeschlossen zu Jewels — beides
/// jeweils erst im Folge-Scan.
/// </summary>
public class AmoebaTests
{
    private const byte Wall = 5;   // TitaniumWall, als Rand für alle Testgitter
    private const byte Stone = 4;  // Wall — mauert die Amoeba ein, ohne Cave-Rand zu sein
    private const byte Empty = 0;
    private const byte Earth = 1;
    private const byte Amoeba = 7;

    private static CaveData BuildCaveData(int width, int height, byte[] tiles, byte amoebaSlowGrowthSeconds = 0) => new()
    {
        Index = 0,
        Name = "Test",
        Description = "",
        Letter = 'A',
        IsIntermission = false,
        Width = (byte)width,
        Height = (byte)height,
        JewelQuota = 0,
        TimeSeconds = 99,
        Colors = [new(0x20, 0x20, 0x20), new(0xFF, 0xFF, 0xFF), new(0xBA, 0x20, 0x20), new(0x71, 0xFF, 0xFF)],
        CameraStartX = 0,
        CameraStartY = 0,
        EnchantedWallSeconds = 0,
        AmoebaSlowGrowthSeconds = amoebaSlowGrowthSeconds,
        PointsPerJewelBeforeQuota = 10,
        PointsPerJewelAfterQuota = 20,
        GameSpeed = CaveSpeed.For(1, isIntermission: false),
        Tiles = tiles,
    };

    private static (Cave Cave, GameState State) Setup(CaveData data)
    {
        var cave = new Cave(data);
        var state = new GameState();
        state.ResetForCave(data);
        return (cave, state);
    }

    private static void Scan(CavePhysics physics, Cave cave, GameState state) =>
        physics.Regel(cave, state, new InputState(), new Camera());

    private static int CountAmoeba(Cave cave)
    {
        var count = 0;
        for (var y = 0; y < cave.Height; y++)
        {
            for (var x = 0; x < cave.Width; x++)
            {
                if (cave.GetElement(x, y) == Element.Amoeba)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>Gitter mit Stahlrand und einer Amoeba in der Mitte; das Innere ist mit
    /// <paramref name="fill"/> gefüllt.</summary>
    private static byte[] BuildBox(int width, int height, byte fill, int amoebaIndex)
    {
        var tiles = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                tiles[(y * width) + x] = border ? Wall : fill;
            }
        }

        tiles[amoebaIndex] = Amoeba;
        return tiles;
    }

    [Fact]
    public void Amoeba_waechst_in_Leerraum_und_in_Erde()
    {
        // Die Amoeba hat genau zwei mögliche Ziele: Leerraum oben, Erde unten.
        byte[] tiles =
        [
            Wall, Wall, Wall,  Wall,  Wall,
            Wall, Wall, Empty, Wall,  Wall,
            Wall, Wall, Amoeba, Wall, Wall,
            Wall, Wall, Earth, Wall,  Wall,
            Wall, Wall, Wall,  Wall,  Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(5, 5, tiles));
        var physics = new CavePhysics(new Random(1));

        // Sobald beide Nachbarn Amoeba sind, ist sie eingeschlossen — ab dem übernächsten Scan wären es
        // Jewels, also hier abbrechen und den Zustand prüfen.
        for (var scan = 0; scan < 500 && CountAmoeba(cave) < 3; scan++)
        {
            Scan(physics, cave, state);
        }

        Assert.Equal(Element.Amoeba, cave.GetElement(2, 1)); // vormals Leerraum
        Assert.Equal(Element.Amoeba, cave.GetElement(2, 3)); // vormals Erde
    }

    [Fact]
    public void Amoeba_waechst_nicht_in_Stein_oder_Mauer()
    {
        var tiles = BuildBox(5, 5, Stone, amoebaIndex: 12);
        var (cave, state) = Setup(BuildCaveData(5, 5, tiles));
        var physics = new CavePhysics(new Random(1));

        Scan(physics, cave, state);

        Assert.Equal(1, CountAmoeba(cave));
    }

    [Fact]
    public void Eingeschlossene_Amoeba_wird_erst_im_Folge_Scan_zu_Jewels()
    {
        var tiles = BuildBox(3, 3, Stone, amoebaIndex: 4);
        var (cave, state) = Setup(BuildCaveData(3, 3, tiles));
        var physics = new CavePhysics(new Random(1));

        // Erster Scan erkennt den Einschluss nur — die Zelle bleibt Amoeba.
        Scan(physics, cave, state);

        Assert.Equal(Element.Amoeba, cave.GetElement(1, 1));
        Assert.True(state.AmoebaSuffocatedLastScan);
        Assert.True(state.AmoebaPresent);

        // Erst der Folge-Scan wandelt um.
        Scan(physics, cave, state);

        Assert.Equal(Element.Jewel, cave.GetElement(1, 1));
        Assert.False(state.AmoebaPresent); // Drone verstummt
    }

    [Fact]
    public void Amoeba_ab_200_Zellen_wird_zu_Boulders()
    {
        // Innenraum 20x10 = genau 200 Zellen, komplett Amoeba und rundum eingemauert.
        var (cave, state) = Setup(BuildCaveData(22, 12, BuildBox(22, 12, Amoeba, amoebaIndex: 23)));
        var physics = new CavePhysics(new Random(1));

        Scan(physics, cave, state);

        Assert.Equal(200, state.AmoebaCountLastScan);
        Assert.Equal(200, CountAmoeba(cave));

        Scan(physics, cave, state);

        Assert.Equal(0, CountAmoeba(cave));
        Assert.Equal(Element.Boulder, cave.GetElement(1, 1));
        Assert.Equal(Element.Boulder, cave.GetElement(20, 10));
    }

    [Fact]
    public void Amoeba_mit_199_Zellen_wird_noch_nicht_zu_Boulders()
    {
        // Dasselbe Gitter, aber eine Innenzelle ist Mauer -> 199 Amoeba-Zellen, weiterhin eingeschlossen.
        var tiles = BuildBox(22, 12, Amoeba, amoebaIndex: 23);
        tiles[(1 * 22) + 1] = Stone;
        var (cave, state) = Setup(BuildCaveData(22, 12, tiles));
        var physics = new CavePhysics(new Random(1));

        Scan(physics, cave, state);

        Assert.Equal(199, state.AmoebaCountLastScan);

        // Zu groß hat Vorrang vor eingeschlossen — hier greift also der Einschluss: Jewels, keine Boulders.
        Scan(physics, cave, state);

        Assert.Equal(Element.Jewel, cave.GetElement(20, 10));
    }

    [Fact]
    public void Amoeba_waechst_nach_Ablauf_der_Amoeba_Zeit_deutlich_schneller()
    {
        // Gleiches Gitter, gleicher Seed, gleiche Scan-Zahl — einziger Unterschied ist die Restzeit:
        // > 0 bedeutet 4/128 (~3 %), 0 bedeutet 4/16 (25 %).
        var langsam = ZellenNachScans(amoebaSlowGrowthSeconds: 100, scans: 15);
        var schnell = ZellenNachScans(amoebaSlowGrowthSeconds: 0, scans: 15);

        Assert.True(langsam >= 1, $"Die langsame Amoeba darf nicht verschwinden, war aber {langsam}.");
        Assert.True(
            schnell > 3 * langsam,
            $"Schnelles Wachstum ({schnell} Zellen) muss deutlich über langsamem ({langsam} Zellen) liegen.");

        static int ZellenNachScans(byte amoebaSlowGrowthSeconds, int scans)
        {
            var data = BuildCaveData(40, 22, BuildBox(40, 22, Empty, amoebaIndex: (11 * 40) + 20), amoebaSlowGrowthSeconds);
            var (cave, state) = Setup(data);
            var physics = new CavePhysics(new Random(1));

            for (var scan = 0; scan < scans; scan++)
            {
                Scan(physics, cave, state);
            }

            return CountAmoeba(cave);
        }
    }

    [Fact]
    public void Amoeba_Zeit_laeuft_in_Spielsekunden_und_synchron_zur_Cave_Zeit()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 10, Empty, Empty, Wall, // Eingang
            Wall, Wall, Wall, Wall, Wall,
        ];
        var data = BuildCaveData(5, 3, tiles, amoebaSlowGrowthSeconds: 50);
        var (cave, state) = Setup(data);
        var random = new Random(1);
        var tick = new GameTick(new CavePhysics(random), new ScreenCover(random));
        var entranceIndex = cave.FindFirstIndexOf(Element.Entrance);
        var input = new InputState();
        var camera = new Camera();
        var clocks = new Clocks();

        // Die ersten 100 Ticks baut der Eingang auf, danach läuft die Spieluhr.
        for (var i = 0; i < 400; i++)
        {
            tick.Tick(cave, state, input, camera, clocks, entranceIndex);
        }

        var caveSekunden = data.TimeSeconds - state.CaveTimeRemaining;
        var amoebaSekunden = data.AmoebaSlowGrowthSeconds - state.AmoebaSlowGrowthRemaining;

        Assert.True(caveSekunden > 0, "Die Cave-Zeit muss überhaupt gelaufen sein.");
        Assert.Equal(caveSekunden, amoebaSekunden);
    }

    [Theory]
    [InlineData((byte)8, Element.Explosion)]       // Firefly explodiert zu Leere
    [InlineData((byte)9, Element.JewelExplosion)]  // Butterfly explodiert zu Jewels
    public void Firefly_und_Butterfly_explodieren_bei_Amoeba_Kontakt(byte kreatur, Element explosion)
    {
        byte[] tiles =
        [
            Wall, Wall,    Wall,   Wall,  Wall,
            Wall, kreatur, Amoeba, Empty, Wall,
            Wall, Wall,    Wall,   Wall,  Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(5, 3, tiles));
        var physics = new CavePhysics(new Random(1));

        Scan(physics, cave, state);

        Assert.Equal(explosion, cave.GetElement(1, 1));
        Assert.Equal(explosion, cave.GetElement(2, 1));
    }

    /// <summary>Die Amoeba-Zeit steht als AmoebaTime in der Cave-Datei — geladen wird, was dort steht.
    /// In BD1 stammt sie aus demselben Cave-Kopf-Byte $01 wie die Zaubermauer-Zeit; dieser Test prüft die
    /// ausgelieferten 100 Dateien dagegen, damit die Daten nicht unbemerkt vom Original abdriften.</summary>
    [Fact]
    public void Alle_Cave_Dateien_tragen_die_BD1_Amoeba_Zeit_ihres_Kopf_Bytes()
    {
        var caves = new CaveTextRepository(Path.Combine(TestPaths.GameAssets, "Caves"));

        for (var letter = 'A'; letter <= 'T'; letter++)
        {
            for (var level = 1; level <= 5; level++)
            {
                var cave = caves.Get($"cave-{letter}-{level}");

                Assert.True(
                    cave.AmoebaSlowGrowthSeconds == cave.EnchantedWallSeconds,
                    $"cave-{letter}-{level}: AmoebaTime {cave.AmoebaSlowGrowthSeconds} weicht von MagicWallTime "
                    + $"{cave.EnchantedWallSeconds} ab — beide stammen aus BD1-Kopf-Byte $01.");
            }
        }

        // Die beiden Amoeba-Caves aus BD1 als Anker (Rohbyte $01: Cave G = 0x4B, Cave M = 0x8C).
        Assert.Equal(75, caves.Get("cave-G-1").AmoebaSlowGrowthSeconds);
        Assert.Equal(140, caves.Get("cave-M-1").AmoebaSlowGrowthSeconds);
    }
}
