using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Data;

/// <summary>Objektnamen, wie sie in den Cave-Textdateien stehen (angelehnt an die BD1-Rohformat-
/// Dokumentation), zugeordnet zu den Element-Werten dieses Ports.</summary>
public static class CaveObjectNames
{
    private static readonly (string Name, Element Element)[] Table =
    [
        ("Space", Element.Empty),
        ("Dirt", Element.Earth),
        ("Wall", Element.Wall),
        ("MagicWall", Element.EnchantedWall),
        ("Outbox", Element.EscapeDoor),
        ("Steel", Element.TitaniumWall),
        ("Firefly", Element.Firefly),
        ("Boulder", Element.Boulder),
        ("Jewel", Element.Jewel),
        ("Inbox", Element.Entrance),
        ("Butterfly", Element.Butterfly),
        ("Rockford", Element.Rockford),
        ("Amoeba", Element.Amoeba),
    ];

    public static string ToName(Element element)
    {
        foreach (var (name, el) in Table)
        {
            if (el == element)
            {
                return name;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(element), element, "Kein Objektname für dieses Element definiert.");
    }

    public static Element FromName(string name)
    {
        foreach (var (n, el) in Table)
        {
            if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
            {
                return el;
            }
        }

        throw new FormatException($"Unbekannter Objektname '{name}'.");
    }
}
