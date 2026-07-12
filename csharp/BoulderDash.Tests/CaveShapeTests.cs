using BoulderDash.Core.Data;
using BoulderDash.Core.Objects;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

/// <summary>
/// Caves ohne Größenbeschränkung und ohne Rechteckzwang: Width/Height spannen nur das Gitter auf, die
/// Höhle darin ist beliebig geformt, und alles um sie herum ist Element.Void — das Nichts außerhalb
/// der Cave, das nicht gezeichnet wird (siehe VoidObject).
///
/// Der Preis dafür ist die Stahlmauer: Die Physik greift ungeprüft auf Nachbarkacheln zu, kein Index
/// darf je aus dem Gitter laufen. Beim Rechteck garantierte das die Randmauer; jetzt muss der Parser
/// den Stahlring prüfen (CaveTextFile.ValidateEnclosure) — die Hälfte dieser Tests deckt genau das ab.
/// </summary>
public class CaveShapeTests
{
    /// <summary>Eine Cave-Datei mit der angegebenen Karte; alles übrige ist Beiwerk.</summary>
    private static string File(int width, int height, params string[] map) =>
        $"""
         [Cave]
         Cave        = Z
         Name        = Form
         Description = Test
         Kind        = Normal
         Level       = 1
         Width       = {width}
         Height      = {height}
         Color1      = #202020
         Color2      = #FFFFFF
         Color3      = #717171
         Color4      = #BA7120

         [Rules]
         JewelsNeeded    = 1
         CaveTime        = 10
         GameSpeed       = 150
         MagicWallTime   = 0
         AmoebaTime      = 0
         JewelValue      = 10
         JewelValueExtra = 15

         [Map]
         {string.Join("\n", map)}
         """;

    /// <summary>Eine achteckige Höhle in einem 12x8-Gitter: schräge Ränder, außen herum Void. Die
    /// unterste Gitterzeile fehlt in der Karte ganz, die Zeilen sind unterschiedlich lang.</summary>
    private static CaveData IrregularCave() => CaveTextFile.Parse(
        File(
            12,
            8,
            "___WWWWWW",
            "__WW....WW",
            "_WW..d...WW",
            "_W.P.....rXW",
            "_WW.......WW",
            "__WW.....WW",
            "___WWWWWWW"),
        "unregelmaessig.txt");

    [Fact]
    public void Karte_darf_kuerzer_sein_als_das_Gitter_der_Rest_ist_Void()
    {
        var cave = IrregularCave();

        // Rechts neben der kürzesten Zeile: Void statt Stahl.
        Assert.Equal(Element.Void, cave.GetElement(11, 0));

        // Die Zeile, die in der Karte gar nicht vorkommt: durchgehend Void.
        for (var x = 0; x < cave.Width; x++)
        {
            Assert.Equal(Element.Void, cave.GetElement(x, 7));
        }
    }

    [Fact]
    public void Die_Hoehle_selbst_steht_unveraendert_im_Gitter()
    {
        var cave = IrregularCave();

        Assert.Equal(Element.Entrance, cave.GetElement(3, 3));
        Assert.Equal(Element.EscapeDoor, cave.GetElement(10, 3));
        Assert.Equal(Element.Jewel, cave.GetElement(5, 2));
        Assert.Equal(Element.TitaniumWall, cave.GetElement(1, 3));
    }

    /// <summary>Ein Loch in der Stahlmauer ist ein Ladefehler, kein Absturz zur Laufzeit: Ohne
    /// lückenlosen Ring liefe ein fallender Stein irgendwann aus dem Gitter.</summary>
    [Fact]
    public void Cave_mit_Loch_in_der_Stahlmauer_wird_abgelehnt()
    {
        var leaky = File(
            12,
            8,
            "___WWWWWW",
            "__WW....WW",
            "_WW..d...WW",
            "_..P.....rXW", // hier fehlt ein Stück Stahl: die Erde stößt ans Nichts
            "_WW.......WW",
            "__WW.....WW",
            "___WWWWWWW");

        var error = Assert.Throws<FormatException>(() => CaveTextFile.Parse(leaky, "undicht.txt"));
        Assert.Contains("nicht lückenlos von Stahl", error.Message);
    }

    /// <summary>Auch andersherum: Void ist genau das, was AUSSERHALB liegt. Ein '_' innerhalb der
    /// Höhle wäre ein Leerraum, der sich bloß nicht zudecken lässt — dafür gibt es das ' '.</summary>
    [Fact]
    public void Void_innerhalb_der_Stahlmauer_wird_abgelehnt()
    {
        var inner = File(
            12,
            8,
            "___WWWWWW",
            "__WW....WW",
            "_WW..d...WW",
            "_W.P._...rXW", // das '_' liegt mitten in der Höhle
            "_WW.......WW",
            "__WW.....WW",
            "___WWWWWWW");

        var error = Assert.Throws<FormatException>(() => CaveTextFile.Parse(inner, "innen.txt"));
        Assert.Contains("INNERHALB", error.Message);
    }

    /// <summary>Eine zu LANGE Zeile bleibt ein Fehler — sie ist ein Vertipper, kein Cave-Umriss
    /// (im Gegensatz zur zu kurzen, die schlicht außerhalb der Höhle endet).</summary>
    [Fact]
    public void Zu_lange_Kartenzeile_wird_abgelehnt()
    {
        var tooWide = File(12, 8, "___WWWWWWWWWWWWW");

        Assert.Throws<FormatException>(() => CaveTextFile.Parse(tooWide, "zu-breit.txt"));
    }

    /// <summary>Die 40x22 des Originals sind keine Grenze mehr: Width/Height waren byte-Felder und
    /// hätten bei 300 Spalten übergelaufen.</summary>
    [Fact]
    public void Cave_darf_groesser_sein_als_ein_Byte_fasst()
    {
        var wide = new string('W', 300);
        var middle = "W" + new string('.', 297) + "PW";
        var big = File(300, 3, wide, middle.Remove(150, 1).Insert(150, "X"), wide);

        var cave = CaveTextFile.Parse(big, "gross.txt");

        Assert.Equal(300, cave.Width);
        Assert.Equal(300 * 3, cave.Tiles.Length);
        Assert.Equal(Element.EscapeDoor, cave.GetElement(150, 1));
    }

    /// <summary>Das Nichts wird nie überzeichnet: Beim Auf- und Zudecken geht die Silhouette der Höhle
    /// auf und zu, nicht das Rechteck des Gitters (CaveObject.CoveredByScreen, ausgeführt im
    /// CaveRenderer).</summary>
    [Fact]
    public void Bildschirm_Verdeckung_laesst_das_Nichts_aus()
    {
        Assert.False(CaveObjects.Prototype(Element.Void).CoveredByScreen);
        Assert.True(CaveObjects.Prototype(Element.Empty).CoveredByScreen);
        Assert.True(CaveObjects.Prototype(Element.TitaniumWall).CoveredByScreen);
    }

    /// <summary>Die einzige Abweichung vom Leerraum, von dem das Nichts erbt: Es hält wie Stahl. Der
    /// 3x3-Schlag einer Explosion erfasst auch die Diagonale und kann dort — bei schräg verlaufender
    /// Mauer — schon im Nichts landen; bliebe davon eine Empty-Kachel zurück, stahlte die Verdeckung
    /// sie am Cave-Ende als einzige Kachel außerhalb der Höhle wieder zu.</summary>
    [Fact]
    public void Das_Nichts_haelt_der_Explosion_stand()
    {
        var cave = TestWorld.NewCave(IrregularCave());
        var outside = cave.IndexOf(0, 0);
        Assert.Equal(Element.Void, cave.GetElement(outside));

        cave.Get(outside).Detonate(() => new ExplosionObject(cave));

        Assert.Equal(Element.Void, cave.GetElement(outside));
    }

    /// <summary>Void ist das 17. Element und passt als erstes nicht mehr in die 4 Bits, die das
    /// Original für die Element-ID hatte — es steht in Bit 0x10 (siehe CaveObjects.ElementMask). Das
    /// Kachelbyte muss den Weg durch das Objekt trotzdem unverändert überstehen, sonst wackelte der
    /// Golden-Hash.</summary>
    [Fact]
    public void Void_ueberlebt_das_Kachelbyte()
    {
        Assert.Equal(0x10, (byte)Element.Void);
        Assert.Equal(0x10, CaveObjects.FromRaw(Cave.Nowhere, 0x10).ToRaw());
        Assert.IsType<VoidObject>(CaveObjects.FromRaw(Cave.Nowhere, 0x10));
    }
}
