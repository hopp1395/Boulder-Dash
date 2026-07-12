using BoulderDash.Core.Data;
using BoulderDash.Core.Objects;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

/// <summary>
/// Das Objektmodell (Core/Objects). Kartenglyphe, Standardframe und Kachelbyte sind früher aus
/// eigenen Tabellen gekommen (SpriteTables.GetDefaultFrame, die Legende in CaveAsciiMap); sie leben
/// jetzt beim Objekt selbst, und die Tabellen werden umgekehrt DARAUS gebaut. Eine Kreuzprobe gegen
/// sie prüfte sich damit selbst — die Erwartungswerte stehen deshalb hier ausgeschrieben.
/// </summary>
public class CaveObjectTests
{
    public static TheoryData<Element> AlleElemente()
    {
        var data = new TheoryData<Element>();
        foreach (var element in Enum.GetValues<Element>())
        {
            data.Add(element);
        }

        return data;
    }

    /// <summary>Das Bild, das ein frisch geladenes, noch nicht angelaufenes Spiel zeigt — die
    /// buffer[MASK_*]-Initialwerte aus Init_Pointer (src/INIT.CPP:187-202). Doppelte Werte sind echt:
    /// Der Ausgang sieht bis zum Öffnen aus wie Stahlwand (12), die Zaubermauer wie eine Ziegelmauer (11).</summary>
    [Theory]
    [InlineData(Element.Empty, 0)]
    [InlineData(Element.Earth, 1)]
    [InlineData(Element.Boulder, 2)]
    [InlineData(Element.Jewel, 3)]
    [InlineData(Element.Wall, 11)]
    [InlineData(Element.TitaniumWall, 12)]
    [InlineData(Element.Rockford, 13)]
    [InlineData(Element.Amoeba, 24)]
    [InlineData(Element.Firefly, 32)]
    [InlineData(Element.Butterfly, 40)]
    [InlineData(Element.Entrance, 48)]
    [InlineData(Element.EscapeDoor, 12)]
    [InlineData(Element.Explosion, 52)]
    [InlineData(Element.EnchantedWall, 11)]
    [InlineData(Element.JewelExplosion, 68)]
    [InlineData(Element.BorderFill, 76)]
    public void Standardframe_jedes_Objekts_steht_fest(Element element, int frame)
    {
        Assert.Equal(frame, CaveObjects.Prototype(element).DefaultFrame);
    }

    /// <summary>Die Legende der Cave-Dateien (identisch zu Boulder-Dash-C64/extracted/caves).
    /// '?' tragen die Objekte, die nur im Spiel entstehen und nie in einer Datei stehen; Rockford
    /// teilt sich das 'P' mit dem Eingang, aus dem er hervorgeht.</summary>
    [Theory]
    [InlineData(Element.Empty, ' ')]
    [InlineData(Element.Earth, '.')]
    [InlineData(Element.Boulder, 'r')]
    [InlineData(Element.Jewel, 'd')]
    [InlineData(Element.Wall, 'w')]
    [InlineData(Element.TitaniumWall, 'W')]
    [InlineData(Element.Rockford, 'P')]
    [InlineData(Element.Amoeba, 'a')]
    [InlineData(Element.Firefly, 'F')]
    [InlineData(Element.Butterfly, 'B')]
    [InlineData(Element.Entrance, 'P')]
    [InlineData(Element.EscapeDoor, 'X')]
    [InlineData(Element.Explosion, '?')]
    [InlineData(Element.EnchantedWall, 'M')]
    [InlineData(Element.JewelExplosion, '?')]
    [InlineData(Element.BorderFill, '?')]
    public void Kartenglyphe_jedes_Objekts_steht_fest(Element element, char glyph)
    {
        Assert.Equal(glyph, CaveObjects.Prototype(element).MapGlyph);
    }

    [Theory]
    [MemberData(nameof(AlleElemente))]
    public void Element_der_erzeugten_Instanz_entspricht_dem_angeforderten(Element element)
    {
        Assert.Equal(element, CaveObjects.Create(Cave.Nowhere, element).Element);
    }

    /// <summary>
    /// Der Nachweis, dass das Objektmodell bit-genau dem alten Bytemodell entspricht: Jedes
    /// Kachelbyte überlebt den Weg durch das Objekt unverändert. Darauf beruht der Golden-Hash
    /// (siehe GoldenCaveScanTests).
    ///
    /// Zwei Objekte tragen beim Erzeugen absichtlich ein zusätzliches Bit und sind deshalb
    /// ausgenommen — sie werden unten einzeln geprüft.
    /// </summary>
    [Theory]
    [MemberData(nameof(AlleElemente))]
    public void Kachelbyte_ueberlebt_den_Weg_durch_das_Objekt(Element element)
    {
        if (element is Element.Butterfly or Element.JewelExplosion)
        {
            return;
        }

        var raw = (byte)element;
        Assert.Equal(raw, CaveObjects.FromRaw(Cave.Nowhere, raw).ToRaw());
    }

    /// <summary>Der Schmetterling bekommt beim Aufbau des Gitters seine Startrichtung "unten"
    /// aufgeprägt (BDCFF 0009) — genau das tat auch der bisherige Cave-Konstruktor mit dem rohen
    /// Byte. Die Diamant-Explosion steht nie in einer Datei und trägt immer das Kreaturen-Bit
    /// (sie entsteht ausschließlich aus einem Schmetterling, der mit 0xCE sprengt).</summary>
    [Fact]
    public void Die_beiden_Objekte_mit_aufgepraegtem_Bit_liefern_ihr_Original_Byte()
    {
        Assert.Equal(0x60 | (byte)Element.Butterfly, CaveObjects.FromRaw(Cave.Nowhere, (byte)Element.Butterfly).ToRaw());
        Assert.Equal(0x40 | (byte)Element.JewelExplosion, new JewelExplosionObject(Cave.Nowhere).ToRaw());
    }

    [Theory]
    [InlineData(0x42)] // fallender Stein  ('R' in der Kartenlegende)
    [InlineData(0x43)] // fallender Diamant ('D')
    public void Fall_Bit_ueberlebt_den_Weg_durch_das_Objekt(byte raw)
    {
        var created = CaveObjects.FromRaw(Cave.Nowhere, raw);

        Assert.True(Assert.IsAssignableFrom<FallingObject>(created).Falling);
        Assert.Equal(raw, created.ToRaw());
    }

    [Fact]
    public void Verarbeitet_Bit_und_Blickrichtung_landen_im_Kachelbyte()
    {
        var firefly = new FireflyObject(Cave.Nowhere) { ScannedThisFrame = true, Facing = CreatureFacing.Right };

        Assert.Equal(0x80 | 0x40 | (byte)Element.Firefly, firefly.ToRaw());
    }

    /// <summary>Die Kreaturen-Explosionen tragen im Original Bit 0x40 (0xCC/0xCE), die
    /// Stein-tötet-Rockford-Explosion nicht (0x8C) — siehe ExplosionObject.CausedByCreature.</summary>
    [Fact]
    public void Explosionsbytes_entsprechen_den_Original_Konstanten()
    {
        Assert.Equal(0xCC, Explosionsbyte(new FireflyObject(Cave.Nowhere)));
        Assert.Equal(0xCE, Explosionsbyte(new ButterflyObject(Cave.Nowhere)));

        // Der Stein, der Rockford erschlägt, sprengt dagegen mit 0x8C.
        Assert.Equal(0x8C, new ExplosionObject(Cave.Nowhere) { ScannedThisFrame = true }.ToRaw());
    }

    /// <summary>Das Byte, mit dem die Kreatur ihre 3x3-Explosion füllt (explosion() setzt dabei stets
    /// auch das Verarbeitet-Bit).</summary>
    private static byte Explosionsbyte(CreatureObject creature)
    {
        var explosion = creature.CreateExplosion();
        explosion.ScannedThisFrame = true;
        return explosion.ToRaw();
    }

    /// <summary>Der Schmetterling startet nach UNTEN blickend (BDCFF 0009), der Geist nach links.</summary>
    [Fact]
    public void Kreaturen_starten_in_ihrer_BD1_Blickrichtung()
    {
        Assert.Equal(CreatureFacing.Down, new ButterflyObject(Cave.Nowhere).Facing);
        Assert.Equal(CreatureFacing.Left, new FireflyObject(Cave.Nowhere).Facing);
    }

    /// <summary>Cave-Explore (siehe ExploreMap): Im Nebel steht nur die erinnerte Umgebung. Genau die
    /// beiden Kreaturen ziehen aus eigenem Antrieb umher und sind dort deshalb unsichtbar — die
    /// Amoeba wuchert zwar auch, gehört aber zum Gelände und bleibt sichtbar.</summary>
    [Theory]
    [MemberData(nameof(AlleElemente))]
    public void Im_Nebel_sind_nur_die_Kreaturen_vergessen(Element element)
    {
        var kreatur = element is Element.Firefly or Element.Butterfly;

        Assert.Equal(!kreatur, CaveObjects.Prototype(element).VisibleInFog);
    }
}
