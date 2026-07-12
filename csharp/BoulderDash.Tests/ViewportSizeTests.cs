using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class ViewportSizeTests
{
    /// <summary>Treue-Wächter: beim Original-Sichtfenster müssen die abgeleiteten Scrollwerte exakt
    /// die Konstanten des Originals ergeben (BOULDER.CPP:893-896, hier je eine Kachel weiter innen).</summary>
    [Fact]
    public void Scrollwerte_entsprechen_beim_Original_Sichtfenster_dem_Original()
    {
        var viewport = ViewportSize.Original;

        Assert.Equal(20, viewport.Columns);
        Assert.Equal(12, viewport.Rows);

        Assert.Equal(16, viewport.ScrollTriggerRight);
        Assert.Equal(8, viewport.ScrollTriggerBottom);
        Assert.Equal(2, ViewportSize.ScrollTriggerNear);
        Assert.Equal(7, viewport.ScrollAmountX);
        Assert.Equal(5, viewport.ScrollAmountY);
    }

    /// <summary>Bei der vollen BD1-Cave im Bild scrollt eine Original-Cave nicht mehr — dafür ist
    /// diese Größe da (und sie ist die Voreinstellung, siehe GameSettings).</summary>
    [Fact]
    public void Volle_Cave_fasst_eine_ganze_BD1_Cave()
    {
        Assert.Equal(new ViewportSize(40, 22), ViewportSize.Full);
    }
}
