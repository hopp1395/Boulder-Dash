using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class CameraTests
{
    /// <summary>Treue-Wächter: Ein Scroll-Schritt bewegt das Sichtfenster um eine Kachel und klemmt
    /// am Cave-Rand (BOULDER.CPP:229-237) — beim Original-Sichtfenster unverändert.</summary>
    [Fact]
    public void Scroll_Schritt_und_Klemmung_bleiben_beim_Original_Sichtfenster_unveraendert()
    {
        var camera = new Camera();
        camera.ResetTo(0, 0);
        camera.Relx = 7;
        camera.Rely = 5;

        camera.Step(40, 22);

        Assert.Equal(1, camera.X);
        Assert.Equal(1, camera.Y);
        Assert.Equal((sbyte)6, camera.Relx);
        Assert.Equal((sbyte)4, camera.Rely);

        // Rechte/untere Klemmung: 40-20 bzw. 22-12.
        camera.ResetTo(39, 21);
        camera.Step(40, 22);
        Assert.Equal(20, camera.X);
        Assert.Equal(10, camera.Y);

        camera.ResetTo(-5, -5);
        camera.Step(40, 22);
        Assert.Equal(0, camera.X);
        Assert.Equal(0, camera.Y);
    }

    /// <summary>Ist das Sichtfenster größer als die Cave (volle Zoomstufe auf einer 20x12-Intermission),
    /// wird die Obergrenze negativ — die Kamera muss dann auf 0 stehen bleiben, nicht ins Minus laufen.
    /// Die Cave wird in diesem Fall beim Zeichnen zentriert (CaveRenderer).</summary>
    [Fact]
    public void Kamera_klemmt_auf_Null_wenn_das_Sichtfenster_groesser_als_die_Cave_ist()
    {
        var camera = new Camera { Viewport = new ViewportSize(40, 22) };
        camera.ResetTo(5, 5);

        camera.Clamp(20, 12);

        Assert.Equal(0, camera.X);
        Assert.Equal(0, camera.Y);
    }

    /// <summary>CenterOn hat die frühere Berechnung aus CaveTextFile abgelöst (CameraStartX/Y) und muss
    /// beim Original-Sichtfenster dieselben Werte liefern: Eingang mittig, an den Cave-Rändern geklemmt.</summary>
    [Theory]
    [InlineData(20, 11, 10, 5)] // mitten in der Cave: Eingang zentriert
    [InlineData(1, 1, 0, 0)] // oben links: Klemmung auf 0
    [InlineData(38, 20, 20, 10)] // unten rechts: Klemmung auf 40-20 bzw. 22-12
    public void CenterOn_zentriert_den_Eingang_wie_die_fruehere_Ladezeit_Berechnung(
        int entranceCol, int entranceRow, int expectedX, int expectedY)
    {
        const int width = 40;
        const int height = 22;
        var camera = new Camera();

        camera.CenterOn(entranceCol, entranceRow, width, height);

        // Die alte Formel aus CaveTextFile (Sichtfenster fest 20x12).
        var oldX = Math.Clamp(entranceCol - (20 / 2), 0, Math.Max(0, width - 20));
        var oldY = Math.Clamp(entranceRow - (12 / 2), 0, Math.Max(0, height - 12));

        Assert.Equal(expectedX, camera.X);
        Assert.Equal(expectedY, camera.Y);
        Assert.Equal(oldX, camera.X);
        Assert.Equal(oldY, camera.Y);
    }

    [Fact]
    public void CenterOn_setzt_den_Scroll_Rest_zurueck()
    {
        var camera = new Camera();
        camera.Relx = 7;
        camera.Rely = -5;

        camera.CenterOn(20, 11, 40, 22);

        Assert.Equal((sbyte)0, camera.Relx);
        Assert.Equal((sbyte)0, camera.Rely);
    }
}
