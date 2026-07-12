using BoulderDash.Core.Data;
using BoulderDash.Core.Objects;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

/// <summary>
/// Das Objektmodell (Core/Objects) gegen die Tabellen geprüft, aus denen es hervorgegangen ist:
/// SpriteTables.GetDefaultFrame und CaveAsciiMap. Solange beide noch existieren, ist das eine
/// Kreuzprobe — sie stellt sicher, dass beim Verteilen der Tabellen auf die Klassen kein Eintrag
/// verlorengegangen oder verrutscht ist.
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

    [Theory]
    [MemberData(nameof(AlleElemente))]
    public void Standardframe_stimmt_mit_der_alten_Frametabelle_ueberein(Element element)
    {
        Assert.Equal(SpriteTables.GetDefaultFrame(element), CaveObjects.Prototype(element).DefaultFrame);
    }

    [Theory]
    [MemberData(nameof(AlleElemente))]
    public void Kartenglyphe_stimmt_mit_der_alten_Legende_ueberein(Element element)
    {
        // CaveAsciiMap liefert '?' für die Objekte, die nie in einer Cave-Datei stehen.
        Assert.Equal(CaveAsciiMap.ToChar(element), CaveObjects.Prototype(element).MapGlyph);
    }

    [Theory]
    [MemberData(nameof(AlleElemente))]
    public void Element_der_erzeugten_Instanz_entspricht_dem_angeforderten(Element element)
    {
        Assert.Equal(element, CaveObjects.Create(element).Element);
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
        Assert.Equal(raw, CaveObjects.FromRaw(raw).ToRaw());
    }

    /// <summary>Der Schmetterling bekommt beim Aufbau des Gitters seine Startrichtung "unten"
    /// aufgeprägt (BDCFF 0009) — genau das tat auch der bisherige Cave-Konstruktor mit dem rohen
    /// Byte. Die Diamant-Explosion steht nie in einer Datei und trägt immer das Kreaturen-Bit
    /// (sie entsteht ausschließlich aus einem Schmetterling, der mit 0xCE sprengt).</summary>
    [Fact]
    public void Die_beiden_Objekte_mit_aufgepraegtem_Bit_liefern_ihr_Original_Byte()
    {
        Assert.Equal(0x60 | (byte)Element.Butterfly, CaveObjects.FromRaw((byte)Element.Butterfly).ToRaw());
        Assert.Equal(0x40 | (byte)Element.JewelExplosion, new JewelExplosionObject().ToRaw());
    }

    [Theory]
    [InlineData(0x42)] // fallender Stein  ('R' in der Kartenlegende)
    [InlineData(0x43)] // fallender Diamant ('D')
    public void Fall_Bit_ueberlebt_den_Weg_durch_das_Objekt(byte raw)
    {
        var created = CaveObjects.FromRaw(raw);

        Assert.True(Assert.IsAssignableFrom<FallingObject>(created).Falling);
        Assert.Equal(raw, created.ToRaw());
    }

    [Fact]
    public void Verarbeitet_Bit_und_Blickrichtung_landen_im_Kachelbyte()
    {
        var firefly = new FireflyObject { Scanned = true, Facing = CreatureFacing.Right };

        Assert.Equal(0x80 | 0x40 | (byte)Element.Firefly, firefly.ToRaw());
    }

    /// <summary>Die Kreaturen-Explosionen tragen im Original Bit 0x40 (0xCC/0xCE), die
    /// Stein-tötet-Rockford-Explosion nicht (0x8C) — siehe ExplosionObject.CausedByCreature.</summary>
    [Fact]
    public void Explosionsbytes_entsprechen_den_Original_Konstanten()
    {
        Assert.Equal(0xCC, Explosionsbyte(new FireflyObject()));
        Assert.Equal(0xCE, Explosionsbyte(new ButterflyObject()));

        // Der Stein, der Rockford erschlägt, sprengt dagegen mit 0x8C.
        Assert.Equal(0x8C, new ExplosionObject { Scanned = true }.ToRaw());
    }

    /// <summary>Das Byte, mit dem die Kreatur ihre 3x3-Explosion füllt (explosion() setzt dabei stets
    /// auch das Verarbeitet-Bit).</summary>
    private static byte Explosionsbyte(CreatureObject creature)
    {
        var explosion = creature.CreateExplosion();
        explosion.Scanned = true;
        return explosion.ToRaw();
    }

    /// <summary>Der Schmetterling startet nach UNTEN blickend (BDCFF 0009), der Geist nach links.</summary>
    [Fact]
    public void Kreaturen_starten_in_ihrer_BD1_Blickrichtung()
    {
        Assert.Equal(CreatureFacing.Down, new ButterflyObject().Facing);
        Assert.Equal(CreatureFacing.Left, new FireflyObject().Facing);
    }
}
