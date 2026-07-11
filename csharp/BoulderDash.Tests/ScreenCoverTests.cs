using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class ScreenCoverTests
{
    private const int Width = 40;
    private const int Height = 22;

    private static ScreenCover NewCover() => new(new BorlandRandom());

    private static IEnumerable<(int X, int Y)> AllCells()
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                yield return (x, y);
            }
        }
    }

    [Fact]
    public void Direkt_nach_BeginUncover_ist_die_gesamte_Cave_verdeckt()
    {
        var cover = NewCover();
        cover.BeginUncover(Width, Height);

        Assert.All(AllCells(), cell => Assert.True(cover.IsCovered(cell.X, cell.Y)));
        Assert.True(cover.IsActive);
    }

    [Fact]
    public void Jede_Runde_deckt_in_jeder_Zeile_mindestens_eine_Zelle_auf()
    {
        // Kern des BD1-Musters: "foreach line in 1..22: randomly choose a horizontal position" —
        // pro Runde bekommt JEDE Zeile eine Zufallsposition, das Aufdecken verteilt sich also
        // gleichmäßig über alle Zeilen (der DOS-Algorithmus zog stattdessen 4 Zellen irgendwo).
        var cover = NewCover();
        cover.BeginUncover(Width, Height);
        cover.Tick();

        for (var y = 0; y < Height; y++)
        {
            var frei = Enumerable.Range(0, Width).Count(x => !cover.IsCovered(x, y));
            Assert.Equal(1, frei);
        }
    }

    [Fact]
    public void Nach_69_Runden_ist_die_ganze_Cave_aufgedeckt_und_die_Animation_beendet()
    {
        // "loop 69 times ... uncover entire screen": die letzte Runde räumt den Rest, den die
        // Zufallswahl übrig gelassen hat.
        var cover = NewCover();
        cover.BeginUncover(Width, Height);

        for (var i = 0; i < ScreenCover.Iterations; i++)
        {
            cover.Tick();
        }

        Assert.All(AllCells(), cell => Assert.False(cover.IsCovered(cell.X, cell.Y)));
        Assert.False(cover.IsActive);
    }

    [Fact]
    public void Nach_69_Runden_Zudecken_ist_die_ganze_Cave_wieder_verdeckt()
    {
        var cover = NewCover();
        cover.BeginUncover(Width, Height);
        for (var i = 0; i < ScreenCover.Iterations; i++)
        {
            cover.Tick();
        }

        cover.BeginCover();
        Assert.True(cover.IsActive);

        // Zu Beginn des Zudeckens ist die Cave noch offen, nach jeder Runde ist mehr verdeckt.
        Assert.All(AllCells(), cell => Assert.False(cover.IsCovered(cell.X, cell.Y)));

        for (var i = 0; i < ScreenCover.Iterations; i++)
        {
            cover.Tick();
        }

        Assert.All(AllCells(), cell => Assert.True(cover.IsCovered(cell.X, cell.Y)));
        Assert.False(cover.IsActive);
    }

    [Fact]
    public void Ohne_geladene_Cave_verdeckt_nichts()
    {
        // Sicherheitsnetz für die Rendering-Schicht: vor dem ersten BeginUncover fragt niemand
        // eine Maske ab, die es noch nicht gibt.
        var cover = NewCover();

        Assert.False(cover.IsActive);
        Assert.False(cover.IsCovered(0, 0));
    }
}
