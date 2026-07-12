using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Erzeugt Cave-Objekte und hält je Element einen Prototyp für die unveränderlichen Angaben
/// (Kartenglyphe, Standardframe). Die einzige Stelle, an der eine Element-ID auf ihre Klasse
/// abgebildet wird — überall sonst arbeitet der Code mit den Objekten selbst.
/// </summary>
public static class CaveObjects
{
    /// <summary>
    /// Die Bits eines Kachelbytes, in denen die Element-ID steckt. Im Original waren es 4 (0x0F) —
    /// mehr als 16 Elemente gab es nicht. Element.Void ist das 17. und braucht deshalb Bit 0x10.
    ///
    /// Weiter geht es nicht: Darüber liegen die Flags — 0x20/0x40 die Blickrichtung der Kreaturen
    /// (CreatureFacing), 0x40 zugleich das Fall-Momentum, 0x80 "verarbeitet". Wer je ein 33. Element
    /// braucht, muss das Kachelbyte verlassen; es dient ohnehin nur noch Serialisierung und
    /// Golden-Hash (siehe CaveObject.ToRaw).
    /// </summary>
    public const byte ElementMask = 0x1F;

    private static readonly CaveObject[] Prototypes = BuildPrototypes();

    /// <summary>
    /// Der Prototyp eines Elements — NUR für die unveränderlichen Angaben (MapGlyph, DefaultFrame).
    /// Er darf niemals in ein Gitter gelegt werden: Cave-Objekte tragen Zustand, ein geteilter
    /// Prototyp würde ihn über alle Kacheln hinweg vermischen. Er lebt deshalb in
    /// <see cref="Cave.Nowhere"/> — einer Höhle ohne Gitter, in der jeder Griff nach einem Nachbarn
    /// ins Leere geht. Fürs Spielgitter ist <see cref="Create"/> zuständig.
    /// </summary>
    public static CaveObject Prototype(Element element) => Prototypes[(byte)element];

    /// <summary>Alle Prototypen, für Registry-getriebene Tabellen (siehe CaveAsciiMap).</summary>
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
    /// 5 Bits (<see cref="ElementMask"/>), dazu das Fall-Bit 0x40 der Sonderglyphen 'R'/'D' (siehe
    /// CaveAsciiMap). Weitere Bits kommen in Cave-Dateien nicht vor.</summary>
    public static CaveObject FromRaw(Cave cave, byte raw)
    {
        var created = New(cave, (Element)(raw & ElementMask));
        if (created is FallingObject falling)
        {
            falling.Falling = (raw & 0x40) != 0;
        }

        return created;
    }

    private static CaveObject New(Cave cave, Element element) => element switch
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
        Element.Void => new VoidObject(cave),
        _ => throw new ArgumentOutOfRangeException(nameof(element), element, "Unbekanntes Element."),
    };

    /// <summary>Ein Prototyp je Element, abgelegt unter seiner ID — die Element-IDs sind lückenlos,
    /// die höchste bestimmt die Länge.</summary>
    private static CaveObject[] BuildPrototypes()
    {
        var elements = Enum.GetValues<Element>();
        var prototypes = new CaveObject[elements.Max(element => (byte)element) + 1];
        foreach (var element in elements)
        {
            prototypes[(byte)element] = New(Cave.Nowhere, element);
        }

        return prototypes;
    }
}
