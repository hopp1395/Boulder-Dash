using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class CaveFileTests
{
    private static readonly string LevelBinPath = Path.Combine(TestPaths.GameAssets, "LEVEL.BIN");

    [Fact]
    public void LoadAll_findet_genau_21_Caves()
    {
        var caves = CaveFile.LoadAll(LevelBinPath);

        Assert.Equal(21, caves.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    [InlineData(20)]
    public void Jede_Cave_hat_plausible_Groesse_und_Geschwindigkeit(int index)
    {
        var cave = CaveFile.LoadAll(LevelBinPath)[index];

        Assert.True(
            (cave.Width, cave.Height) is (40, 22) or (20, 12),
            $"Cave {cave.Letter}: unerwartete Größe {cave.Width}x{cave.Height}");
        Assert.True(cave.GameSpeed is 1 or 2, $"Cave {cave.Letter}: unerwarteter game_speed {cave.GameSpeed}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    // Index 20 bewusst ausgenommen: siehe Cave_20_ist_ein_leerer_Platzhalter.
    public void Jede_Cave_hat_genau_einen_Eingang_und_einen_Ausgang(int index)
    {
        var cave = CaveFile.LoadAll(LevelBinPath)[index];

        var entrances = cave.Tiles.Count(t => t == (byte)Element.Entrance);
        var exits = cave.Tiles.Count(t => t == (byte)Element.EscapeDoor);

        Assert.True(entrances == 1, $"Cave {cave.Letter}: {entrances} Eingänge statt 1");
        Assert.True(exits == 1, $"Cave {cave.Letter}: {exits} Ausgänge statt 1");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    // Index 20 bewusst ausgenommen: siehe Cave_20_ist_ein_leerer_Platzhalter.
    public void Jede_Cave_hat_einen_geschlossenen_Rand(int index)
    {
        // Der Rand besteht überwiegend aus Stahl, einige Caves platzieren den Ausgang aber
        // direkt in der Randwand (z.B. Cave C: Ausgang bei x=39=Breite-1). Beides ist ein
        // "solides" Randelement; wichtig ist nur, dass der Rand geschlossen bleibt (kein Erde,
        // Stein, Diamant o.ä., das aus dem Feld herausfallen/-rollen könnte).
        var cave = CaveFile.LoadAll(LevelBinPath)[index];

        void AssertRandelement(int x, int y)
        {
            var element = cave.GetElement(x, y);
            Assert.True(
                element is Element.TitaniumWall or Element.EscapeDoor,
                $"Cave {cave.Letter} bei ({x},{y}): unerwartetes Randelement {element}");
        }

        for (var x = 0; x < cave.Width; x++)
        {
            AssertRandelement(x, 0);
            AssertRandelement(x, cave.Height - 1);
        }

        for (var y = 0; y < cave.Height; y++)
        {
            AssertRandelement(0, y);
            AssertRandelement(cave.Width - 1, y);
        }
    }

    [Fact]
    public void Cave_0_heisst_A_wie_im_Original_Menue()
    {
        var cave = CaveFile.LoadAll(LevelBinPath)[0];

        Assert.Equal('A', cave.Letter);
    }

    [Fact]
    public void Cave_20_ist_ein_leerer_Platzhalter()
    {
        // Das originale LEVEL.BIN reserviert 21 Datensatzplätze (passend zum Wrap "cavenr==21→0"
        // im Menü, BOULDER.CPP:313), aber der 21. (Index 20) enthält nur Nullbytes als
        // Kachelkarte — ein nie fertiggestellter Platzhalter, konsistent mit dem
        // "In Entwicklung"-Kommentar direkt nach level_laden (BOULDER.CPP:1043).
        var cave = CaveFile.LoadAll(LevelBinPath)[20];

        Assert.All(cave.Tiles, t => Assert.Equal((byte)Element.Empty, t));
    }
}
