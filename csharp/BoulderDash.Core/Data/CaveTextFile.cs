using System.Globalization;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Data;

/// <summary>
/// Parser für das menschenlesbare Cave-Textformat: '#'-Zeilen und Leerzeilen werden ignoriert,
/// `[Section]`-Header schalten den Abschnitt um, darin stehen `Key = Value`-Paare. Die Kachelkarte
/// steht als ASCII-Raster im [Map]-Abschnitt (Legende siehe CaveAsciiMap) und ist die maßgebliche
/// Kartenquelle — was in der Datei steht, wird genau so geladen.
/// </summary>
public static class CaveTextFile
{
    /// <summary>Sichtfenster der Kamera in Kacheln (siehe Camera.Step).</summary>
    private const int ViewportWidth = 20;
    private const int ViewportHeight = 12;

    public static CaveData Parse(string text, string sourceName)
    {
        var caveFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rulesFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var mapLines = new List<(string Line, int LineNumber)>();

        var section = "";
        var lines = text.Split('\n');
        for (var lineNumber = 1; lineNumber <= lines.Length; lineNumber++)
        {
            // Kartenzeilen dürfen führende/abschließende Leerzeichen enthalten (Leerraum-Kacheln),
            // daher hier nur den Zeilenumbruch entfernen und erst für die Struktur-Erkennung trimmen.
            var raw = lines[lineNumber - 1].TrimEnd('\r');
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                section = trimmed[1..^1].Trim();
                continue;
            }

            switch (section)
            {
                case "Cave":
                    ParseKeyValue(trimmed, sourceName, lineNumber, caveFields);
                    break;
                case "Rules":
                    ParseKeyValue(trimmed, sourceName, lineNumber, rulesFields);
                    break;
                case "Map":
                    mapLines.Add((raw, lineNumber));
                    break;
                default:
                    throw new FormatException($"{sourceName}:{lineNumber}: Zeile außerhalb eines bekannten Abschnitts.");
            }
        }

        return BuildCaveData(sourceName, caveFields, rulesFields, mapLines);
    }

    private static void ParseKeyValue(string line, string sourceName, int lineNumber, Dictionary<string, string> target)
    {
        var separator = line.IndexOf('=');
        if (separator < 0)
        {
            throw new FormatException($"{sourceName}:{lineNumber}: 'Key = Value' erwartet, gefunden: '{line}'.");
        }

        target[line[..separator].Trim()] = line[(separator + 1)..].Trim();
    }

    private static CaveData BuildCaveData(
        string sourceName,
        Dictionary<string, string> caveFields,
        Dictionary<string, string> rulesFields,
        List<(string Line, int LineNumber)> mapLines)
    {
        var kind = RequireField(caveFields, "Kind", sourceName);
        var isIntermission = kind switch
        {
            "Normal" => false,
            "Intermission" => true,
            _ => throw new FormatException($"{sourceName}: Kind muss 'Normal' oder 'Intermission' sein, gefunden: '{kind}'."),
        };

        var colorsText = RequireField(rulesFields, "Colors", sourceName).Split(',', StringSplitOptions.TrimEntries);
        if (colorsText.Length != 3)
        {
            throw new FormatException($"{sourceName}: Colors muss genau 3 kommagetrennte Werte haben.");
        }

        var letter = char.ToUpperInvariant(RequireField(caveFields, "Cave", sourceName)[0]);
        var width = RequireByte(caveFields, "Width", sourceName);
        var height = RequireByte(caveFields, "Height", sourceName);
        var tiles = ParseMap(sourceName, mapLines, width, height);

        var entranceIndex = Array.IndexOf(tiles, (byte)Element.Entrance);
        if (entranceIndex < 0)
        {
            throw new FormatException($"{sourceName}: [Map] enthält keinen Eingang ('P').");
        }

        // Kamera so setzen, dass der Eingang möglichst mittig im Sichtfenster liegt.
        var cameraStartX = Math.Clamp((entranceIndex % width) - (ViewportWidth / 2), 0, Math.Max(0, width - ViewportWidth));
        var cameraStartY = Math.Clamp((entranceIndex / width) - (ViewportHeight / 2), 0, Math.Max(0, height - ViewportHeight));

        return new CaveData
        {
            Index = letter - 'A',
            Name = RequireField(caveFields, "Name", sourceName),
            Description = RequireField(caveFields, "Description", sourceName),
            Letter = letter,
            IsIntermission = isIntermission,
            Width = width,
            Height = height,
            JewelQuota = RequireByte(rulesFields, "JewelsNeeded", sourceName),
            TimeSeconds = RequireByte(rulesFields, "CaveTime", sourceName),
            // Konvention des ursprünglichen 1999er-Ports (empirisch aus LEVEL.BIN übernommen):
            // BaseColors=[0,1,Farbe2,Farbe1] - die dritte BD1-Rohfarbe bleibt ungenutzt.
            BaseColors = [0, 1, ParseByte(colorsText[1], sourceName, "Colors"), ParseByte(colorsText[0], sourceName, "Colors")],
            CameraStartX = (byte)cameraStartX,
            CameraStartY = (byte)cameraStartY,
            EnchantedWallSeconds = RequireByte(rulesFields, "MagicWallTime", sourceName),
            PointsPerJewelBeforeQuota = RequireByte(rulesFields, "JewelValue", sourceName),
            PointsPerJewelAfterQuota = RequireByte(rulesFields, "JewelValueExtra", sourceName),
            GameSpeed = 1,
            Tiles = tiles,
        };
    }

    private static byte[] ParseMap(string sourceName, List<(string Line, int LineNumber)> mapLines, byte width, byte height)
    {
        if (mapLines.Count != height)
        {
            throw new FormatException($"{sourceName}: [Map] hat {mapLines.Count} Kartenzeilen, erwartet werden {height} (Height).");
        }

        var tiles = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            var (line, lineNumber) = mapLines[y];
            if (line.Length != width)
            {
                throw new FormatException(
                    $"{sourceName}:{lineNumber}: Kartenzeile ist {line.Length} Zeichen lang, erwartet werden {width} (Width).");
            }

            for (var x = 0; x < width; x++)
            {
                if (!CaveAsciiMap.TryToElement(line[x], out var element))
                {
                    throw new FormatException($"{sourceName}:{lineNumber}: unbekanntes Kartenzeichen '{line[x]}' an Spalte {x}.");
                }

                tiles[(y * width) + x] = (byte)element;
            }
        }

        return tiles;
    }

    private static string RequireField(Dictionary<string, string> fields, string key, string sourceName) =>
        fields.TryGetValue(key, out var value) ? value : throw new FormatException($"{sourceName}: Pflichtfeld '{key}' fehlt.");

    private static byte RequireByte(Dictionary<string, string> fields, string key, string sourceName) =>
        ParseByte(RequireField(fields, key, sourceName), sourceName, key);

    private static byte ParseByte(string token, string sourceName, string key) =>
        byte.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException($"{sourceName}: '{key}' erwartet eine Zahl 0-255, gefunden: '{token}'.");
}
