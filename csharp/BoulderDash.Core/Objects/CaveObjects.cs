using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Erzeugt Cave-Objekte und hält je Element einen Prototyp für die unveränderlichen Angaben
/// (Kartenglyphe, Standardframe). Die einzige Stelle, an der eine Element-ID auf ihre Klasse
/// abgebildet wird — überall sonst arbeitet der Code mit den Objekten selbst.
/// </summary>
public static class CaveObjects
{
    private static readonly CaveObject[] Prototypes = BuildPrototypes();

    /// <summary>
    /// Der Prototyp eines Elements — NUR für die unveränderlichen Angaben (MapGlyph, DefaultFrame).
    /// Er gehört zu keiner Cave und darf niemals in ein Gitter gelegt werden: Cave-Objekte tragen
    /// Zustand und brauchen ihre Welt; ein geteilter Prototyp würde beides vermischen. Er wehrt sich
    /// dagegen — jeder Zugriff auf seine Cave wirft. Fürs Gitter ist <see cref="Create"/> zuständig.
    /// </summary>
    public static CaveObject Prototype(Element element) => Prototypes[(byte)element];

    /// <summary>Alle 16 Prototypen, für Registry-getriebene Tabellen (siehe CaveAsciiMap).</summary>
    public static IEnumerable<CaveObject> All => Prototypes;

    /// <summary>Eine frische Instanz für eine Cave. <paramref name="animationPhase"/> ist deren
    /// aktuelle Phase: Ein neu entstandenes Objekt (eine gewachsene Amoeba-Zelle, ein aus der
    /// Zaubermauer fallender Diamant) übernimmt sie, damit es im Gleichtakt mit allen übrigen
    /// animiert und nicht sichtbar aus der Reihe tanzt.</summary>
    public static CaveObject Create(Cave cave, Element element, byte animationPhase = 0)
    {
        var created = New(cave, element);
        created.AnimationPhase = animationPhase;
        return created;
    }

    /// <summary>Baut ein Objekt aus einem Kachelbyte der Cave-Datei: Element-ID in den unteren
    /// 4 Bits, dazu das Fall-Bit 0x40 der Sonderglyphen 'R'/'D' (siehe CaveAsciiMap). Weitere Bits
    /// kommen in Cave-Dateien nicht vor.</summary>
    public static CaveObject FromRaw(Cave cave, byte raw)
    {
        var created = New(cave, (Element)(raw & 0x0F));
        if (created is FallingObject falling)
        {
            falling.Falling = (raw & 0x40) != 0;
        }

        return created;
    }

    private static CaveObject New(Cave? cave, Element element) => element switch
    {
        Element.Empty => new EmptyObject(cave),
        Element.Earth => new EarthObject(cave),
        Element.Boulder => new BoulderObject(cave),
        Element.Jewel => new JewelObject(cave),
        Element.Wall => new WallObject(cave),
        Element.TitaniumWall => new TitaniumWallObject(cave),
        Element.Rockford => new RockfordObject(cave),
        Element.Amoeba => new AmoebaObject(cave),
        Element.Firefly => new FireflyObject(cave),
        Element.Butterfly => new ButterflyObject(cave),
        Element.Entrance => new EntranceObject(cave),
        Element.EscapeDoor => new EscapeDoorObject(cave),
        Element.Explosion => new ExplosionObject(cave),
        Element.EnchantedWall => new EnchantedWallObject(cave),
        Element.JewelExplosion => new JewelExplosionObject(cave),
        Element.BorderFill => new BorderFillObject(cave),
        _ => throw new ArgumentOutOfRangeException(nameof(element), element, "Unbekanntes Element."),
    };

    private static CaveObject[] BuildPrototypes()
    {
        var prototypes = new CaveObject[16];
        for (var i = 0; i < prototypes.Length; i++)
        {
            prototypes[i] = New(null, (Element)i);
        }

        return prototypes;
    }
}
