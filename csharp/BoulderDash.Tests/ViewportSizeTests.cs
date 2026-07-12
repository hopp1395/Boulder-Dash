using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class ViewportSizeTests
{
    /// <summary>Vom Original-Sichtfenster bis zum Vierfachen der vollen BD1-Cave: bis zu ihr (40x22)
    /// in kleinen Schritten, darüber in halben BD1-Caves.</summary>
    [Fact]
    public void Stufen_reichen_vom_Original_bis_zum_Vierfachen_der_vollen_Cave()
    {
        Assert.Equal(ViewportSize.Original, ViewportSize.Steps[0]);
        Assert.Equal(new ViewportSize(20, 12), ViewportSize.Steps[0]);
        Assert.Equal(new ViewportSize(160, 88), ViewportSize.Steps[^1]);
        Assert.Contains(ViewportSize.Full, ViewportSize.Steps);

        // Jede Stufe wächst — bis zur vollen Cave um 4 Spalten/2 Zeilen, darüber um deren Hälfte.
        for (var i = 1; i < ViewportSize.Steps.Count; i++)
        {
            var (previous, step) = (ViewportSize.Steps[i - 1], ViewportSize.Steps[i]);
            var (columns, rows) = previous.Columns < ViewportSize.Full.Columns ? (4, 2) : (20, 11);

            Assert.Equal(previous.Columns + columns, step.Columns);
            Assert.Equal(previous.Rows + rows, step.Rows);
        }
    }

    [Fact]
    public void Naechste_und_vorige_Stufe_klemmen_an_den_Enden()
    {
        Assert.Equal(ViewportSize.Steps[1], ViewportSize.Original.NextLarger());
        Assert.Equal(ViewportSize.Original, ViewportSize.Steps[1].NextSmaller());

        // Unter das Original geht es nicht (kleineres Bild macht der Bildschirm-Zoom) und über die
        // größte Stufe hinaus auch nicht.
        Assert.Equal(ViewportSize.Original, ViewportSize.Original.NextSmaller());
        Assert.Equal(ViewportSize.Steps[^1], ViewportSize.Steps[^1].NextLarger());

        // Über die volle Cave hinaus geht es weiter — dort wechselt nur die Schrittweite.
        Assert.Equal(new ViewportSize(60, 33), ViewportSize.Full.NextLarger());
        Assert.Equal(ViewportSize.Full, new ViewportSize(60, 33).NextSmaller());
    }

    [Theory]
    [InlineData(20, 12, 20, 12)]
    [InlineData(1, 1, 20, 12)] // viel zu klein -> kleinste Stufe
    [InlineData(999, 999, 160, 88)] // viel zu groß -> größte Stufe
    [InlineData(26, 15, 24, 14)] // krumme Werte -> nächstgelegene Stufe
    [InlineData(85, 47, 80, 44)] // auch oberhalb der vollen Cave
    public void Snap_liefert_die_naechstgelegene_Stufe(int columns, int rows, int expectedColumns, int expectedRows)
    {
        Assert.Equal(new ViewportSize(expectedColumns, expectedRows), ViewportSize.Snap(columns, rows));
    }

    /// <summary>Treue-Wächter: beim Original-Sichtfenster müssen die abgeleiteten Scrollwerte exakt
    /// die Konstanten des Originals ergeben (BOULDER.CPP:893-896, hier je eine Kachel weiter innen).</summary>
    [Fact]
    public void Scrollwerte_entsprechen_beim_Original_Sichtfenster_dem_Original()
    {
        var viewport = ViewportSize.Original;

        Assert.Equal(16, viewport.ScrollTriggerRight);
        Assert.Equal(8, viewport.ScrollTriggerBottom);
        Assert.Equal(2, ViewportSize.ScrollTriggerNear);
        Assert.Equal(7, viewport.ScrollAmountX);
        Assert.Equal(5, viewport.ScrollAmountY);
    }

    /// <summary>Der Scrollsprung darf nie über den Auslöser hinausschießen — sonst würde die Kamera
    /// über Rockford hinweg scrollen statt ihn wieder zur Mitte zu holen.</summary>
    [Fact]
    public void Scrollweite_bleibt_auf_jeder_Stufe_kleiner_als_der_Ausloeser()
    {
        foreach (var viewport in ViewportSize.Steps)
        {
            Assert.InRange(viewport.ScrollAmountX, 1, viewport.ScrollTriggerRight - 1);
            Assert.InRange(viewport.ScrollAmountY, 1, viewport.ScrollTriggerBottom - 1);
        }
    }
}
