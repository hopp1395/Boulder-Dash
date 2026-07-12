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

    /// <summary>Farben je Cave — ein Sprite-Pixel trägt einen Palettenindex 0-3 (siehe SpriteTextFile).</summary>
    private const int PaletteSize = 4;

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

        var colors = ParseColors(sourceName, RequireField(rulesFields, "Colors", sourceName));

        var letter = char.ToUpperInvariant(RequireField(caveFields, "Cave", sourceName)[0]);

        // Der Schwierigkeitsgrad steht im Cave-Kopf und bestimmt (zusammen mit Kind) allein das
        // Spieltempo — in BD1 gibt es kein Tempo pro Cave (siehe CaveSpeed).
        var level = RequireByte(caveFields, "Level", sourceName);
        if (level is < 1 or > 5)
        {
            throw new FormatException($"{sourceName}: Level muss 1..5 sein, gefunden: {level}.");
        }

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
            Colors = colors,
            CameraStartX = (byte)cameraStartX,
            CameraStartY = (byte)cameraStartY,
            EnchantedWallSeconds = RequireByte(rulesFields, "MagicWallTime", sourceName),
            AmoebaSlowGrowthSeconds = RequireByte(rulesFields, "AmoebaTime", sourceName),
            PointsPerJewelBeforeQuota = RequireByte(rulesFields, "JewelValue", sourceName),
            PointsPerJewelAfterQuota = RequireByte(rulesFields, "JewelValueExtra", sourceName),
            // Millisekunden pro Cave-Scan; in BD1 aus Level und Kind abgeleitet, steht aber wie alle
            // Spieldaten in der Datei selbst (siehe CaveSpeed).
            GameSpeed = CaveSpeed.FromScanMilliseconds(RequireInt(rulesFields, "GameSpeed", sourceName, 20, 1000)),
            Tiles = tiles,
        };
    }

    /// <summary>
    /// Liest das Colors-Feld: 4 RGB-Werte (#RRGGBB) in Palettenreihenfolge, also die Farben der
    /// Palettenindizes 0-3, mit denen die Sprites der Cave eingefärbt werden (siehe Palette).
    /// </summary>
    private static Rgb[] ParseColors(string sourceName, string field)
    {
        var tokens = field.Split(',', StringSplitOptions.TrimEntries);
        if (tokens.Length != PaletteSize)
        {
            throw new FormatException($"{sourceName}: Colors muss genau {PaletteSize} kommagetrennte RGB-Werte haben.");
        }

        var colors = new Rgb[PaletteSize];
        for (var i = 0; i < tokens.Length; i++)
        {
            if (!Rgb.TryParse(tokens[i], out colors[i]))
            {
                throw new FormatException($"{sourceName}: 'Colors' erwartet RGB-Werte im Format #RRGGBB, gefunden: '{tokens[i]}'.");
            }
        }

        return colors;
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
                if (!CaveAsciiMap.TryToRaw(line[x], out var raw))
                {
                    throw new FormatException($"{sourceName}:{lineNumber}: unbekanntes Kartenzeichen '{line[x]}' an Spalte {x}.");
                }

                tiles[(y * width) + x] = raw;
            }
        }

        return tiles;
    }

    private static string RequireField(Dictionary<string, string> fields, string key, string sourceName) =>
        fields.TryGetValue(key, out var value) ? value : throw new FormatException($"{sourceName}: Pflichtfeld '{key}' fehlt.");

    private static byte RequireByte(Dictionary<string, string> fields, string key, string sourceName) =>
        ParseByte(RequireField(fields, key, sourceName), sourceName, key);

    private static int RequireInt(Dictionary<string, string> fields, string key, string sourceName, int min, int max)
    {
        var token = RequireField(fields, key, sourceName);
        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
        {
            throw new FormatException($"{sourceName}: '{key}' erwartet eine Zahl {min}-{max}, gefunden: '{token}'.");
        }

        return value;
    }

    private static byte ParseByte(string token, string sourceName, string key) =>
        byte.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException($"{sourceName}: '{key}' erwartet eine Zahl 0-255, gefunden: '{token}'.");
}
