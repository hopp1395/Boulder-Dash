using BoulderDash.Core.Objects;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Data;

/// <summary>
/// Legende der ASCII-Kartendarstellung (identisch zu Boulder-Dash-C64/extracted/caves/cave_*.txt) in
/// beide Richtungen: Render() erzeugt die Kartenzeilen einer fertigen Cave, TryToRaw() liest ein
/// Kartenzeichen zurück (verwendet vom [Map]-Parser in CaveTextFile).
///
/// Die Legende selbst steht nicht mehr hier, sondern bei den Objekten (CaveObject.MapGlyph) — diese
/// Klasse baut daraus nur noch die Nachschlagetabellen. Übrig bleiben die Fälle, die keinem Objekt
/// gehören: die Zweitschreibweise des Ausgangs und die Fall-Bit-Sonderglyphen.
/// </summary>
public static class CaveAsciiMap
{
    private static readonly Dictionary<Element, char> ElementToChar = BuildElementToChar();

    private static readonly Dictionary<char, Element> CharToElement = BuildCharToElement();

    /// <summary>Sonderglyphen, die ein ROHES Kachelbyte setzen statt nur ein Element — nämlich ein
    /// Objekt mit bereits gesetztem Fall-Bit. Gedacht für die Prüfstand-Caves: manche Zustände
    /// entstehen im normalen Spiel nur für einen einzigen Cave-Scan und lassen sich sonst nicht
    /// gezielt herstellen (siehe cave-test-6.txt). In den 100 BD1-Caves kommen sie nicht vor.</summary>
    private static readonly Dictionary<char, byte> CharToFallingRaw = new()
    {
        ['R'] = 0x40 | (byte)Element.Boulder,
        ['D'] = 0x40 | (byte)Element.Jewel,
    };

    public static char ToChar(Element element) => ElementToChar.GetValueOrDefault(element, '?');

    public static bool TryToElement(char c, out Element element) => CharToElement.TryGetValue(c, out element);

    /// <summary>Kartenzeichen zum rohen Kachelbyte — die maßgebliche Richtung für den [Map]-Parser.
    /// Deckt die normale Legende ab (Rohbyte = Element-ID) plus die Fall-Bit-Sonderglyphen.</summary>
    public static bool TryToRaw(char c, out byte raw)
    {
        if (CharToFallingRaw.TryGetValue(c, out raw))
        {
            return true;
        }

        if (TryToElement(c, out var element))
        {
            raw = (byte)element;
            return true;
        }

        raw = 0;
        return false;
    }

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

    private static Dictionary<Element, char> BuildElementToChar()
    {
        var map = new Dictionary<Element, char>();
        foreach (var prototype in CaveObjects.All)
        {
            if (prototype.MapGlyph != '?')
            {
                map[prototype.Element] = prototype.MapGlyph;
            }
        }

        return map;
    }

    private static Dictionary<char, Element> BuildCharToElement()
    {
        var map = new Dictionary<char, Element>();
        foreach (var prototype in CaveObjects.All)
        {
            // Rockford teilt sich die Glyphe 'P' mit dem Eingang und steht nie selbst in einer Datei
            // — er entsteht erst aus dem Eingang (siehe EntranceObject). Beim Lesen gewinnt deshalb
            // der Eingang.
            if (prototype.MapGlyph == '?' || prototype.Element == Element.Rockford)
            {
                continue;
            }

            map[prototype.MapGlyph] = prototype.Element;
        }

        // Im Original ist 'x' der blinkende (aktive) Ausgang - dieselbe Kachel, siehe extract_data.py.
        map['x'] = Element.EscapeDoor;

        return map;
    }
}
