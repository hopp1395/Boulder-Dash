using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

/// <summary>
/// Die vier Farben einer Cave stehen als Color1-Color4 im [Cave]-Abschnitt der Cave-Datei, je ein
/// RGB-Wert (WYSIWYG) — es gibt keine Farbtabelle mehr, aus der ein Index nachgeschlagen würde.
/// </summary>
public class CaveColorsTests
{
    private static string CaveText(string colorLines) => $"""
        [Cave]
        Cave        = A
        Name        = Test
        Description = Test
        Kind        = Normal
        Level       = 1
        Width       = 3
        Height      = 3
        {colorLines}

        [Rules]
        JewelsNeeded    = 1
        CaveTime        = 99
        GameSpeed       = 150
        MagicWallTime   = 0
        AmoebaTime      = 0
        JewelValue      = 10
        JewelValueExtra = 15

        [Map]
        WWW
        WPX
        WWW
        """;

    private const string GueltigeFarben = """
        Color1      = #202020
        Color2      = #FFFFFF
        Color3      = #717171
        Color4      = #BA7120
        """;

    [Fact]
    public void Color1_bis_Color4_werden_als_RGB_Werte_in_Palettenreihenfolge_gelesen()
    {
        var cave = CaveTextFile.Parse(CaveText(GueltigeFarben), "test");

        Assert.Equal(
            [new Rgb(0x20, 0x20, 0x20), new Rgb(0xFF, 0xFF, 0xFF), new Rgb(0x71, 0x71, 0x71), new Rgb(0xBA, 0x71, 0x20)],
            cave.Colors);
    }

    [Theory]
    // fehlendes Farbfeld
    [InlineData("Color1      = #202020\nColor2      = #FFFFFF\nColor3      = #717171")]
    // alter Farbindex statt RGB
    [InlineData("Color1      = #202020\nColor2      = #FFFFFF\nColor3      = #717171\nColor4      = 8")]
    // unvollständiger RGB-Wert
    [InlineData("Color1      = #202020\nColor2      = #FFFFFF\nColor3      = #717171\nColor4      = #BA712")]
    // altes Sammelfeld aus [Cave] heraus
    [InlineData("Colors      = #202020, #FFFFFF, #717171, #BA7120")]
    public void Ungueltige_Farbfelder_werden_abgelehnt(string colorLines)
    {
        Assert.Throws<FormatException>(() => CaveTextFile.Parse(CaveText(colorLines), "test"));
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
