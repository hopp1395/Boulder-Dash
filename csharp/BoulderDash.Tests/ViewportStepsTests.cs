using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

/// <summary>Die Zoomstufen des Spielflächen-Zooms, abgeleitet aus der Zeichenfläche (ViewportSteps).
/// Die Pixelmaße der Zeichenschicht sind CaveRenderer.TileSize/StatusLineHeight; Core kennt sie
/// nicht, die Tests reichen sie wie die Schale herein.</summary>
public class ViewportStepsTests
{
    private const int TileSize = 16;
    private const int StatusLineHeight = 8;

    private static ViewportSteps For(int width, int height) =>
        ViewportSteps.For(width, height, TileSize, StatusLineHeight);

    /// <summary>Jede Stufe muss bei ihrem ganzzahligen Kachelmaßstab auf die Fläche passen — das ist
    /// der ganze Sinn der Ableitung: kein krummer Maßstab, kein Abrunden, kein toter Rand.</summary>
    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(3840, 2160)]
    [InlineData(1366, 768)]
    [InlineData(1280, 720)]
    public void Jede_Stufe_passt_ganzzahlig_auf_die_Flaeche(int width, int height)
    {
        var steps = For(width, height);

        foreach (var viewport in steps.Sizes.Skip(1)) // ohne das Original, das immer dabei ist
        {
            var logicalWidth = viewport.Columns * TileSize;
            var logicalHeight = StatusLineHeight + (viewport.Rows * TileSize);
            var scale = Math.Min(width / logicalWidth, height / logicalHeight);

            Assert.True(scale >= 1);
            Assert.True(logicalWidth * scale <= width);
            Assert.True(logicalHeight * scale <= height);

            // Und es ist wirklich die größte Stufe zu diesem Maßstab: eine Kachel mehr passte nicht.
            Assert.True(((viewport.Columns + 1) * TileSize * scale) > width
                || (StatusLineHeight + ((viewport.Rows + 1) * TileSize)) * scale > height);
        }
    }

    /// <summary>Die Enden der Leiter: unten das Original-Sichtfenster, oben der native Maßstab 1x.</summary>
    [Fact]
    public void Leiter_reicht_vom_Original_bis_zum_nativen_Massstab()
    {
        var steps = For(1920, 1080);

        Assert.Equal(ViewportSize.Original, steps.Sizes[0]);

        // 1x: 1920/16 Spalten, (1080-8)/16 Zeilen.
        Assert.Equal(new ViewportSize(120, 67), steps.Sizes[^1]);
    }

    /// <summary>Auf 1920x1080 fällt die volle BD1-Cave genau auf den Maßstab 3x — die Voreinstellung
    /// (GameSettings) ist dort also eine echte Stufe und wird nicht weggerundet.</summary>
    [Fact]
    public void Volle_Cave_ist_auf_1920x1080_eine_eigene_Stufe()
    {
        var steps = For(1920, 1080);

        Assert.Equal(
            [new(20, 12), new(24, 13), new(30, 16), new(40, 22), new(60, 33), new(120, 67)],
            steps.Sizes);
        Assert.Contains(ViewportSize.Full, steps.Sizes);
    }

    /// <summary>Die Stufen wachsen streng — sonst könnte ein Zoomschritt nichts ändern.</summary>
    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(1366, 768)]
    [InlineData(800, 600)]
    public void Stufen_wachsen_streng(int width, int height)
    {
        var sizes = For(width, height).Sizes;

        for (var i = 1; i < sizes.Count; i++)
        {
            Assert.True(sizes[i].Columns > sizes[i - 1].Columns);
            Assert.True(sizes[i].Rows > sizes[i - 1].Rows);
        }
    }

    /// <summary>Eine Fläche, auf die nicht einmal das Original ganzzahlig passt, hat genau eine Stufe:
    /// das Original. Der Bildschirm-Zoom rechnet es dann herunter (BoulderDashGame.GetScale).</summary>
    [Fact]
    public void Winzige_Flaeche_behaelt_das_Original_als_einzige_Stufe()
    {
        var steps = For(320, 200);

        Assert.Equal([ViewportSize.Original], steps.Sizes);
        Assert.Equal(ViewportSize.Original, steps.Larger(ViewportSize.Original));
        Assert.Equal(ViewportSize.Original, steps.Smaller(ViewportSize.Original));
    }

    [Fact]
    public void Hinein_und_hinaus_klemmen_an_den_Enden()
    {
        var steps = For(1920, 1080);
        var sizes = steps.Sizes;

        Assert.Equal(sizes[1], steps.Larger(sizes[0]));
        Assert.Equal(sizes[0], steps.Smaller(sizes[1]));

        Assert.Equal(sizes[0], steps.Smaller(sizes[0]));
        Assert.Equal(sizes[^1], steps.Larger(sizes[^1]));
    }

    /// <summary>Snap fängt Größen ab, die es auf dieser Fläche nicht gibt — den Wunschwert aus der
    /// Einstellungsdatei und die alte Stufe nach einer Größenänderung des Fensters.</summary>
    [Theory]
    [InlineData(40, 22, 40, 22)] // eine echte Stufe bleibt
    [InlineData(26, 15, 24, 13)] // krumm -> nächstgelegene
    [InlineData(1, 1, 20, 12)] // viel zu klein -> kleinste
    [InlineData(999, 999, 120, 67)] // viel zu groß -> größte
    public void Snap_liefert_die_naechstgelegene_Stufe(int columns, int rows, int expectedColumns, int expectedRows)
    {
        var steps = For(1920, 1080);

        Assert.Equal(new ViewportSize(expectedColumns, expectedRows), steps.Snap(new ViewportSize(columns, rows)));
    }

    /// <summary>Der Scrollsprung darf nie über den Auslöser hinausschießen — sonst würde die Kamera
    /// über Rockford hinweg scrollen statt ihn wieder zur Mitte zu holen. Gilt für jede Stufe, die
    /// irgendein Bildschirm hergeben kann.</summary>
    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(3840, 2160)]
    [InlineData(1366, 768)]
    [InlineData(800, 600)]
    public void Scrollweite_bleibt_auf_jeder_Stufe_kleiner_als_der_Ausloeser(int width, int height)
    {
        foreach (var viewport in For(width, height).Sizes)
        {
            Assert.InRange(viewport.ScrollAmountX, 1, viewport.ScrollTriggerRight - 1);
            Assert.InRange(viewport.ScrollAmountY, 1, viewport.ScrollTriggerBottom - 1);
        }
    }
}
