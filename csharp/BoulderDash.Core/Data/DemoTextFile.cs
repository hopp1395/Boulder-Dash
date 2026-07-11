using System.Globalization;

namespace BoulderDash.Core.Data;

/// <summary>Ein Demo-Zug: die Richtung, die Rockford für genau eine clk_1-Periode
/// (den Takt von Mov_Rockford) hält.</summary>
public enum DemoStep
{
    Wait,
    Right,
    Left,
    Down,
    Up,
}

/// <summary>
/// Parser für das menschenlesbare Demo-Textformat: '#'-Zeilen und Leerzeilen werden ignoriert,
/// jede andere Zeile ist ein Zug "Richtung Anzahl" (Anzahl optional, Standard 1), z.B.
/// "Right 7" oder "Wait 15". Das entspricht dem Original-BD1-Demoformat (ein Byte je Zug:
/// unteres Nibble = Richtung, oberes Nibble = Wiederholungszahl), nur ausgeschrieben statt
/// hexadezimal. Die Züge werden zu einem flachen Schritt-Strom expandiert — ein Eintrag pro
/// clk_1-Periode, konsumiert vom DemoPlayer.
/// </summary>
public static class DemoTextFile
{
    public static IReadOnlyList<DemoStep> Load(string path) => Parse(File.ReadAllText(path), path);

    public static IReadOnlyList<DemoStep> Parse(string text, string sourceName)
    {
        var steps = new List<DemoStep>();

        var lines = text.Split('\n');
        for (var lineNumber = 1; lineNumber <= lines.Length; lineNumber++)
        {
            var trimmed = lines[lineNumber - 1].Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var tokens = trimmed.Split(' ', '\t', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > 2)
            {
                throw new FormatException($"{sourceName}:{lineNumber}: 'Richtung [Anzahl]' erwartet, gefunden: '{trimmed}'.");
            }

            if (!Enum.TryParse<DemoStep>(tokens[0], ignoreCase: true, out var step))
            {
                throw new FormatException(
                    $"{sourceName}:{lineNumber}: unbekannte Richtung '{tokens[0]}' (erlaubt: Wait, Right, Left, Down, Up).");
            }

            var count = 1;
            if (tokens.Length == 2
                && (!int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 1))
            {
                throw new FormatException($"{sourceName}:{lineNumber}: Anzahl muss eine Zahl >= 1 sein, gefunden: '{tokens[1]}'.");
            }

            for (var i = 0; i < count; i++)
            {
                steps.Add(step);
            }
        }

        return steps;
    }
}
