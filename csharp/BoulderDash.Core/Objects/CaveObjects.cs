using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Erzeugt Cave-Objekte und hält je Element einen Prototyp für die unveränderlichen Angaben
/// (Kartenglyphe, Standardframe). Die einzige Stelle, an der die Element-ID auf ihre Klasse
/// abgebildet wird — überall sonst arbeitet der Code mit den Objekten selbst.
/// </summary>
public static class CaveObjects
{
    private static readonly CaveObject[] Prototypes = BuildPrototypes();

    /// <summary>
    /// Der Prototyp eines Elements — NUR für die unveränderlichen Angaben (MapGlyph, DefaultFrame,
    /// Prädikate ohne Zustandsbezug). Er darf niemals in ein Cave-Gitter gelegt werden: Cave-Objekte
    /// tragen Zustand, ein geteilter Prototyp würde ihn über alle Kacheln hinweg vermischen. Fürs
    /// Gitter ist <see cref="Create"/> zuständig.
    /// </summary>
    public static CaveObject Prototype(Element element) => Prototypes[(byte)element];

    /// <summary>Alle 16 Prototypen, für Registry-getriebene Tabellen (siehe CaveAsciiMap).</summary>
    public static IEnumerable<CaveObject> All => Prototypes;

    /// <summary>Eine frische Instanz fürs Gitter. <paramref name="animationPhase"/> ist die aktuelle
    /// Cave-Phase: Ein neu entstandenes Objekt (eine gewachsene Amoeba-Zelle, ein aus der Zaubermauer
    /// fallender Diamant) übernimmt sie, damit es im Gleichtakt mit allen übrigen animiert und nicht
    /// sichtbar aus der Reihe tanzt.</summary>
    public static CaveObject Create(Element element, byte animationPhase = 0)
    {
        var created = New(element);
        created.AnimationPhase = animationPhase;
        return created;
    }

    /// <summary>Baut ein Objekt aus einem Kachelbyte der Cave-Datei: Element-ID in den unteren
    /// 4 Bits, dazu das Fall-Bit 0x40 der Sonderglyphen 'R'/'D' (siehe CaveAsciiMap). Weitere Bits
    /// kommen in Cave-Dateien nicht vor.</summary>
    public static CaveObject FromRaw(byte raw)
    {
        var created = New((Element)(raw & 0x0F));
        if (created is FallingObject falling)
        {
            falling.Falling = (raw & 0x40) != 0;
        }

        return created;
    }

    private static CaveObject New(Element element) => element switch
    {
        Element.Empty => new EmptyObject(),
        Element.Earth => new EarthObject(),
        Element.Boulder => new BoulderObject(),
        Element.Jewel => new JewelObject(),
        Element.Wall => new WallObject(),
        Element.TitaniumWall => new TitaniumWallObject(),
        Element.Rockford => new RockfordObject(),
        Element.Amoeba => new AmoebaObject(),
        Element.Firefly => new FireflyObject(),
        Element.Butterfly => new ButterflyObject(),
        Element.Entrance => new EntranceObject(),
        Element.EscapeDoor => new EscapeDoorObject(),
        Element.Explosion => new ExplosionObject(),
        Element.EnchantedWall => new EnchantedWallObject(),
        Element.JewelExplosion => new JewelExplosionObject(),
        Element.BorderFill => new BorderFillObject(),
        _ => throw new ArgumentOutOfRangeException(nameof(element), element, "Unbekanntes Element."),
    };

    private static CaveObject[] BuildPrototypes()
    {
        var prototypes = new CaveObject[16];
        for (var i = 0; i < prototypes.Length; i++)
        {
            prototypes[i] = New((Element)i);
        }

        return prototypes;
    }
}
