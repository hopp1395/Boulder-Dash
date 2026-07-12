using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

/// <summary>
/// Die vier Farben einer Cave stehen als RGB-Werte in der Cave-Datei (WYSIWYG) — es gibt keine
/// Farbtabelle mehr, aus der ein Index nachgeschlagen würde.
/// </summary>
public class CaveColorsTests
{
    private static string CaveText(string colors) => $"""
        [Cave]
        Cave        = A
        Name        = Test
        Description = Test
        Kind        = Normal
        Level       = 1
        Width       = 3
        Height      = 3

        [Rules]
        JewelsNeeded    = 1
        CaveTime        = 99
        GameSpeed       = 150
        MagicWallTime   = 0
        AmoebaTime      = 0
        JewelValue      = 10
        JewelValueExtra = 15
        Colors          = {colors}

        [Map]
        WWW
        WPW
        WWW
        """;

    [Fact]
    public void Colors_wird_als_vier_RGB_Werte_in_Palettenreihenfolge_gelesen()
    {
        var cave = CaveTextFile.Parse(CaveText("#202020, #FFFFFF, #717171, #BA7120"), "test");

        Assert.Equal(
            [new Rgb(0x20, 0x20, 0x20), new Rgb(0xFF, 0xFF, 0xFF), new Rgb(0x71, 0x71, 0x71), new Rgb(0xBA, 0x71, 0x20)],
            cave.Colors);
    }

    [Theory]
    [InlineData("#202020, #FFFFFF, #717171")]          // zu wenige Farben
    [InlineData("#202020, #FFFFFF, #717171, #BA7120, #FFFF20")] // zu viele Farben
    [InlineData("#202020, #FFFFFF, #717171, 8")]       // alter Farbindex statt RGB
    [InlineData("#202020, #FFFFFF, #717171, #BA712")]  // unvollständiger RGB-Wert
    public void Ungueltiges_Colors_Feld_wird_abgelehnt(string colors)
    {
        Assert.Throws<FormatException>(() => CaveTextFile.Parse(CaveText(colors), "test"));
    }

    /// <summary>Sicherung der Umrechnung der früheren 16-Farben-Tabelle: Cave A/1 stand auf den
    /// Indizes 8 und 11 (DAC-Tripel 46,28,8 bzw. 28,28,28) und muss weiterhin genau so aussehen.</summary>
    [Fact]
    public void Ausgelieferte_Caves_tragen_die_umgerechneten_BD1_Farben()
    {
        var caves = new CaveTextRepository(Path.Combine(TestPaths.GameAssets, "Caves"));

        Assert.Equal(
            [new Rgb(0x20, 0x20, 0x20), new Rgb(0xFF, 0xFF, 0xFF), new Rgb(0x71, 0x71, 0x71), new Rgb(0xBA, 0x71, 0x20)],
            caves.Get("cave-A-1").Colors);

        for (var letter = 'A'; letter <= 'T'; letter++)
        {
            for (var level = 1; level <= 5; level++)
            {
                Assert.Equal(4, caves.Get($"cave-{letter}-{level}").Colors.Length);
            }
        }
    }
}
