using System.Globalization;

namespace BoulderDash.Core.Data;

/// <summary>
/// Eine geparste Sprite-Textdatei: Kopfdaten plus Animationsframes eines Objekts
/// (siehe SpriteTextFile). Die Frames sind rohe Pixeldaten, wie sie SpriteAtlas einfärbt.
/// </summary>
public sealed class SpriteData
{
    public required string Name { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }

    /// <summary>Ein byte[] je Frame, zeilenweise, Länge = Width*Height, ein Byte = Palettenindex 0-3.</summary>
    public required IReadOnlyList<byte[]> Frames { get; init; }
}

/// <summary>
/// Parser für das menschenlesbare Sprite-Textformat: eine Datei pro Objekt (benannt nach den
/// BDCFF-Objektnamen), darin ein [Sprite]-Kopf mit `Key = Value`-Paaren und je Animationsframe
/// ein [Frame N]-Abschnitt mit einem Zeichen pro Pixel: `.` `:` `x` `#` = Palettenindex 0-3
/// (dieselbe Konvention wie die C64-Extraktionen in Boulder-Dash-C64/extracted/sprites).
/// '#'-Kommentarzeilen und Leerzeilen werden nur AUSSERHALB der [Frame]-Abschnitte ignoriert —
/// innerhalb eines Frames ist jede nicht-leere Zeile eine Pixelzeile, weil '#' zugleich das
/// Glyph für Farbe 3 ist und eine Pixelzeile damit beginnen darf.
/// </summary>
public static class SpriteTextFile
{
    /// <summary>Glyph je Pixelwert; die Position im String ist der Palettenindex.</summary>
    private const string Glyphs = ".:x#";

    /// <summary>Glyph für einen Pixelwert 0-3 (für Werkzeuge, die das Format schreiben).</summary>
    public static char ToChar(byte pixel) => Glyphs[pixel];

    public static SpriteData Parse(string text, string sourceName)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var frames = new List<(List<(string Row, int LineNumber)> Rows, int LineNumber)>();
        var inFrame = false;

        var lines = text.Split('\n');
        for (var lineNumber = 1; lineNumber <= lines.Length; lineNumber++)
        {
            var raw = lines[lineNumber - 1].TrimEnd('\r');
            var trimmed = raw.Trim();

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                var section = trimmed[1..^1].Trim();
                if (section.Equals("Sprite", StringComparison.OrdinalIgnoreCase))
                {
                    inFrame = false;
                }
                else if (section.StartsWith("Frame", StringComparison.OrdinalIgnoreCase))
                {
                    var numberText = section["Frame".Length..].Trim();
                    if (!int.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                        || number != frames.Count + 1)
                    {
                        throw new FormatException(
                            $"{sourceName}:{lineNumber}: [Frame]-Abschnitte müssen lückenlos ab 1 nummeriert sein, erwartet wird [Frame {frames.Count + 1}].");
                    }

                    frames.Add(([], lineNumber));
                    inFrame = true;
                }
                else
                {
                    throw new FormatException($"{sourceName}:{lineNumber}: unbekannter Abschnitt '[{section}]'.");
                }

                continue;
            }

            if (trimmed.Length == 0)
            {
                continue;
            }

            if (inFrame)
            {
                frames[^1].Rows.Add((raw, lineNumber));
                continue;
            }

            if (trimmed.StartsWith('#'))
            {
                continue;
            }

            var separator = trimmed.IndexOf('=');
            if (separator < 0)
            {
                throw new FormatException($"{sourceName}:{lineNumber}: 'Key = Value' erwartet, gefunden: '{trimmed}'.");
            }

            fields[trimmed[..separator].Trim()] = trimmed[(separator + 1)..].Trim();
        }

        var width = RequireInt(fields, "Width", sourceName);
        var height = RequireInt(fields, "Height", sourceName);
        var frameCount = RequireInt(fields, "Frames", sourceName);
        if (frames.Count != frameCount)
        {
            throw new FormatException($"{sourceName}: {frames.Count} [Frame]-Abschnitte gefunden, Frames = {frameCount} angegeben.");
        }

        return new SpriteData
        {
            Name = RequireField(fields, "Name", sourceName),
            Width = width,
            Height = height,
            Frames = frames.Select(frame => ParseFrame(sourceName, frame.Rows, frame.LineNumber, width, height)).ToArray(),
        };
    }

    private static byte[] ParseFrame(
        string sourceName, List<(string Row, int LineNumber)> rows, int headerLineNumber, int width, int height)
    {
        if (rows.Count != height)
        {
            throw new FormatException(
                $"{sourceName}:{headerLineNumber}: Frame hat {rows.Count} Pixelzeilen, erwartet werden {height} (Height).");
        }

        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            var (row, lineNumber) = rows[y];
            if (row.Length != width)
            {
                throw new FormatException(
                    $"{sourceName}:{lineNumber}: Pixelzeile ist {row.Length} Zeichen lang, erwartet werden {width} (Width).");
            }

            for (var x = 0; x < width; x++)
            {
                var value = Glyphs.IndexOf(row[x]);
                if (value < 0)
                {
                    throw new FormatException(
                        $"{sourceName}:{lineNumber}: unbekanntes Pixelzeichen '{row[x]}' an Spalte {x} (erlaubt: {Glyphs}).");
                }

                pixels[(y * width) + x] = (byte)value;
            }
        }

        return pixels;
    }

    private static string RequireField(Dictionary<string, string> fields, string key, string sourceName) =>
        fields.TryGetValue(key, out var value) ? value : throw new FormatException($"{sourceName}: Pflichtfeld '{key}' fehlt.");

    private static int RequireInt(Dictionary<string, string> fields, string key, string sourceName) =>
        int.TryParse(RequireField(fields, key, sourceName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException($"{sourceName}: '{key}' erwartet eine Zahl, gefunden: '{fields[key]}'.");
}
