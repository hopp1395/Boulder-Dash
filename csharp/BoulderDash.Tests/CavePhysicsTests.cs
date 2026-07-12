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
        AmoebaSlowGrowthSeconds = 0,
        PointsPerJewelBeforeQuota = pointsBefore,
        PointsPerJewelAfterQuota = pointsAfter,
        GameSpeed = CaveSpeed.For(1, isIntermission: false),
        Tiles = tiles,
    };

    private static (BoulderDash.Core.Simulation.Cave Cave, GameState State) Setup(CaveData data)
    {
        var cave = new BoulderDash.Core.Simulation.Cave(data);
        var state = new GameState();
        state.ResetForCave(data);
        return (cave, state);
    }

    private static CavePhysics NewPhysics() => new(new Random(1));

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

        NewPhysics().Regel(cave, state, new InputState(), new Camera());

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

        NewPhysics().Regel(cave, state, new InputState(), new Camera());

        // Links blockiert (Stein bei (1,2), kein leerer Diagonalplatz), also Rollen nach rechts.
        Assert.Equal(Element.Empty, cave.GetElement(2, 1));
        Assert.Equal(Element.Boulder, cave.GetElement(3, 1));
    }

    /// <summary>Abgerollt wird nur von RUHENDEN runden Objekten (BDCFF 0000). Liegt darunter ein
    /// FALLENDER Stein, bleibt der obere liegen, statt zur Seite auszuweichen.</summary>
    [Fact]
    public void Boulder_rollt_nicht_von_einem_fallenden_Boulder_ab()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 0, 2, 0, Wall, // ruhender Stein...
            Wall, 0, 0x42, 0, Wall, // ...auf einem FALLENDEN Stein (Bit 0x40)
            Wall, 0, 0, 0, Wall, // links/rechts wäre Platz zum Abrollen
            Wall, Wall, Wall, Wall, Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(5, 5, tiles));

        NewPhysics().Regel(cave, state, new InputState(), new Camera());

        Assert.Equal(Element.Boulder, cave.GetElement(2, 1)); // bleibt liegen, rollt nicht nach links
        Assert.Equal(Element.Empty, cave.GetElement(1, 1));
        Assert.Equal(Element.Boulder, cave.GetElement(2, 3)); // der untere fällt normal weiter
    }

    /// <summary>Butterfly startet nach BD1 nach unten blickend und sucht seine Vorzugsrichtung im
    /// Uhrzeigersinn — im freien Feld zieht er deshalb zuerst nach LINKS (BDCFF 0009). Der Firefly
    /// startet nach links und sucht gegen den Uhrzeigersinn, zieht also zuerst nach UNTEN (BDCFF 0008).</summary>
    [Theory]
    [InlineData((byte)9, 1, 2)] // Butterfly -> links
    [InlineData((byte)8, 2, 3)] // Firefly   -> unten
    public void Kreatur_zieht_im_freien_Feld_zuerst_in_ihre_Startvorzugsrichtung(
        byte kreatur, int erwartetX, int erwartetY)
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 0, 0, 0, Wall,
            Wall, 0, kreatur, 0, Wall,
            Wall, 0, 0, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(5, 5, tiles));

        NewPhysics().Regel(cave, state, new InputState(), new Camera());

        Assert.Equal(Element.Empty, cave.GetElement(2, 2));
        Assert.Equal((Element)kreatur, cave.GetElement(erwartetX, erwartetY));
    }

    /// <summary>Sind Vorzugsrichtung UND geradeaus versperrt, dreht die Kreatur sich genau einmal zur
    /// Gegenseite und bleibt diesen Scan stehen (BDCFF 0008). Hier steckt die Kreatur in einer nach
    /// rechts offenen Sackgasse: der Firefly (links blickend) kann weder nach unten (Vorzug) noch nach
    /// links, dreht also nach oben und zieht erst im zweiten Scan dorthin.</summary>
    [Fact]
    public void Firefly_in_der_Sackgasse_dreht_einmal_und_zieht_erst_im_naechsten_Scan()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, Wall, 0, 0, Wall, // oben offen
            Wall, Wall, 8, 0, Wall, // Firefly, links versperrt
            Wall, Wall, Wall, 0, Wall, // unten versperrt
            Wall, Wall, Wall, Wall, Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(5, 5, tiles));
        var physics = NewPhysics();

        physics.Regel(cave, state, new InputState(), new Camera());

        // Erster Scan: nur gedreht, nicht gezogen.
        Assert.Equal(Element.Firefly, cave.GetElement(2, 2));

        physics.Regel(cave, state, new InputState(), new Camera());

        // Zweiter Scan: zieht in die neue Blickrichtung (oben).
        Assert.Equal(Element.Empty, cave.GetElement(2, 2));
        Assert.Equal(Element.Firefly, cave.GetElement(2, 1));
    }

    /// <summary>Eine Kreatur zündet auch an einem Rockford, der sich in DIESEM Scan schon bewegt hat
    /// und deshalb das Verarbeitet-Bit trägt (BDCFF: "Rockford, scanned this frame"). Rockford steht
    /// links der Kreatur und läuft ihr entgegen; da er in der Scan-Reihenfolge vor ihr liegt, sieht sie
    /// ihn bereits als 0x86. Das DOS-Original prüfte beim Butterfly mit 0xFE und übersah das.</summary>
    [Theory]
    [InlineData((byte)9, Element.JewelExplosion)] // Butterfly -> Jewels
    [InlineData((byte)8, Element.Explosion)]      // Firefly   -> Leere
    public void Kreatur_explodiert_an_einem_im_selben_Scan_bewegten_Rockford(byte kreatur, Element explosion)
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 0, 0, 0, Wall,
            Wall, 6, 0, kreatur, Wall, // Rockford links, Kreatur rechts, dazwischen frei
            Wall, 0, 0, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(5, 5, tiles));
        var input = new InputState();
        input.PressRight(); // Rockford tritt neben die Kreatur

        NewPhysics().Regel(cave, state, input, new Camera());

        Assert.Equal(explosion, cave.GetElement(3, 2)); // Kreatur explodiert
        Assert.Equal(explosion, cave.GetElement(2, 2)); // Rockford wird mitgerissen
    }

    /// <summary>Der Butterfly dreht bei Blockade zur GEGENSEITE seiner Vorzugsrichtung, also gegen den
    /// Uhrzeigersinn. In einer nur nach oben offenen Sackgasse braucht er dadurch zwei Drehungen
    /// (unten -> rechts -> oben) und zieht erst im dritten Scan. Das DOS-Original drehte hier
    /// fälschlich auf die Vorzugsseite und zog schon im zweiten Scan.</summary>
    [Fact]
    public void Butterfly_in_der_Sackgasse_dreht_gegen_den_Uhrzeigersinn()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, Wall, 0, Wall, Wall, // nur nach oben offen
            Wall, Wall, 9, Wall, Wall, // Butterfly, blickt anfangs nach unten
            Wall, Wall, Wall, Wall, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(5, 5, tiles));
        var physics = NewPhysics();

        // Scan 1: unten (Vorzug) und links versperrt -> dreht nach rechts, kein Zug.
        physics.Regel(cave, state, new InputState(), new Camera());
        Assert.Equal(Element.Butterfly, cave.GetElement(2, 2));

        // Scan 2: rechts blickend sind unten (Vorzug) und rechts versperrt -> dreht nach oben, kein Zug.
        physics.Regel(cave, state, new InputState(), new Camera());
        Assert.Equal(Element.Butterfly, cave.GetElement(2, 2));

        // Scan 3: oben blickend ist rechts (Vorzug) versperrt, geradeaus frei -> zieht nach oben.
        physics.Regel(cave, state, new InputState(), new Camera());
        Assert.Equal(Element.Empty, cave.GetElement(2, 2));
        Assert.Equal(Element.Butterfly, cave.GetElement(2, 1));
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

        NewPhysics().Regel(cave, state, input, new Camera());

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

        NewPhysics().Regel(cave, state, input, new Camera());

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

        NewPhysics().Regel(cave, state, input, new Camera());

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

        NewPhysics().Regel(cave, state, input, new Camera());

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

        new CavePhysics(new AlwaysHits()).Regel(cave, state, input, new Camera());

        Assert.Equal(Element.Empty, cave.GetElement(1, 1));
        Assert.Equal(Element.Rockford, cave.GetElement(2, 1));
        Assert.Equal(Element.Boulder, cave.GetElement(3, 1));
    }

    /// <summary>Der Schub gelingt nur mit 1:8 pro Versuch (BDCFF 0006) — geht der Wurf daneben,
    /// bleibt alles stehen. Das DOS-Original hatte hier ein festes Clk4-Fenster statt eines Wurfs.</summary>
    [Fact]
    public void Stein_schiebt_nicht_wenn_der_Wurf_danebengeht()
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

        new CavePhysics(new NeverHits()).Regel(cave, state, input, new Camera());

        Assert.Equal(Element.Rockford, cave.GetElement(1, 1)); // unverändert, kein Schub
        Assert.Equal(Element.Boulder, cave.GetElement(2, 1));
    }

    /// <summary>Ein FALLENDER Stein lässt sich nicht schieben ("he cannot push falling boulders",
    /// BDCFF 0006) — er fällt einfach weiter, auch wenn der Wurf gelingen würde.</summary>
    [Fact]
    public void Fallender_Stein_laesst_sich_nicht_schieben()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall, Wall,
            Wall, 6, 0x42, 0, 0, Wall, // Stein mit Fall-Momentum neben Rockford
            Wall, Wall, 0, 0, 0, Wall, // darunter frei -> er fällt wirklich
            Wall, Wall, Wall, Wall, Wall, Wall,
        ];
        var (cave, state) = Setup(BuildCaveData(6, 4, tiles));
        var input = new InputState();
        input.PressRight();

        new CavePhysics(new AlwaysHits()).Regel(cave, state, input, new Camera());

        Assert.Equal(Element.Rockford, cave.GetElement(1, 1)); // Rockford bleibt stehen
        Assert.Equal(Element.Empty, cave.GetElement(3, 1)); // nichts dahinter geschoben
        Assert.Equal(Element.Boulder, cave.GetElement(2, 2)); // der Stein fällt stattdessen weiter
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

        NewPhysics().Regel(cave, state, new InputState(), new Camera());

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

        NewPhysics().Regel(cave, state, new InputState(), new Camera());

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

        NewPhysics().Regel(cave, state, input, camera);

        Assert.Equal(Element.Rockford, cave.GetElement(1, 2)); // keine Bewegung ausgeführt
        Assert.Equal(Element.Earth, cave.GetElement(2, 2)); // Erde unverändert
        Assert.Equal((sbyte)-5, camera.Rely); // Scroll-Ziel wurde trotzdem gesetzt
    }

    /// <summary>Würfelt immer die 0 — jeder 1:8-Wurf (Schieben) gelingt.</summary>
    private sealed class AlwaysHits : Random
    {
        public override int Next(int maxValue) => 0;
    }

    /// <summary>Würfelt nie die 0 — jeder 1:8-Wurf (Schieben) geht daneben.</summary>
    private sealed class NeverHits : Random
    {
        public override int Next(int maxValue) => maxValue - 1;
    }
}
