using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Data;

/// <summary>
/// Legende der ASCII-Kartendarstellung (identisch zu Boulder-Dash-C64/extracted/caves/cave_*.txt)
/// in beide Richtungen: Render() erzeugt die Kartenzeilen einer fertigen Cave, ToElement() liest ein
/// Kartenzeichen zurück (verwendet vom [Map]-Parser in CaveTextFile).
/// </summary>
public static class CaveAsciiMap
{
    private static readonly Dictionary<Element, char> ElementToChar = new()
    {
        [Element.TitaniumWall] = 'W',
        [Element.Wall] = 'w',
        [Element.EnchantedWall] = 'M',
        [Element.Earth] = '.',
        [Element.Empty] = ' ',
        [Element.Boulder] = 'r',
        [Element.Jewel] = 'd',
        [Element.Entrance] = 'P',
        [Element.EscapeDoor] = 'X',
        [Element.Firefly] = 'F',
        [Element.Butterfly] = 'B',
        [Element.Amoeba] = 'a',
        [Element.Rockford] = 'P',
    };

    private static readonly Dictionary<char, Element> CharToElement = new()
    {
        ['W'] = Element.TitaniumWall,
        ['w'] = Element.Wall,
        ['M'] = Element.EnchantedWall,
        ['.'] = Element.Earth,
        [' '] = Element.Empty,
        ['r'] = Element.Boulder,
        ['d'] = Element.Jewel,
        ['P'] = Element.Entrance,
        ['X'] = Element.EscapeDoor,
        // 'x' ist im Original der blinkende (aktive) Ausgang - dieselbe Kachel, siehe extract_data.py.
        ['x'] = Element.EscapeDoor,
        ['F'] = Element.Firefly,
        ['B'] = Element.Butterfly,
        ['a'] = Element.Amoeba,
    };

    public static char ToChar(Element element) => ElementToChar.TryGetValue(element, out var c) ? c : '?';

    public static bool TryToElement(char c, out Element element) => CharToElement.TryGetValue(c, out element);

    public static string[] Render(CaveData cave)
    {
        var lines = new string[cave.Height];
        for (var y = 0; y < cave.Height; y++)
        {
            var chars = new char[cave.Width];
            for (var x = 0; x < cave.Width; x++)
            {
                chars[x] = ToChar(cave.GetElement(x, y));
            }

            lines[y] = new string(chars);
        }

        return lines;
    }
}
