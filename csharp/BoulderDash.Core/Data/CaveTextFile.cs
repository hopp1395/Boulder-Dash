using System.Globalization;
using BoulderDash.Core.Objects;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Data;

/// <summary>
/// Parser für das menschenlesbare Cave-Textformat: '#'-Zeilen und Leerzeilen werden ignoriert,
/// `[Section]`-Header schalten den Abschnitt um, darin stehen `Key = Value`-Paare. Die Kachelkarte
/// steht als ASCII-Raster im [Map]-Abschnitt (Legende siehe CaveAsciiMap) und ist die maßgebliche
/// Kartenquelle — was in der Datei steht, wird genau so geladen.
///
/// Width/Height spannen dabei nur das GITTER auf, nicht die Cave: Die Höhle darin darf beliebig
/// geformt sein und beliebig groß (die 40x22 des Originals sind keine Grenze mehr). Was die Karte
/// nicht nennt — kürzere Zeilen, fehlende Zeilen —, liegt außerhalb der Höhle und wird Element.Void
/// (siehe <see cref="ParseMap"/>). Umschlossen sein muss sie trotzdem: Auf dem lückenlosen Stahlring
/// beruht die ganze Physik, und der Parser besteht darauf (siehe <see cref="ValidateEnclosure"/>).
/// </summary>
public static class CaveTextFile
{
    /// <summary>Farben je Cave — ein Sprite-Pixel trägt einen Palettenindex 0-3 (siehe SpriteTextFile).</summary>
    private const int PaletteSize = 4;

    /// <summary>Obergrenze für Width/Height. KEINE Spielregel — eine Cave darf beliebig groß sein —,
    /// sondern eine Notbremse gegen den Vertipper: Width und Height spannen das Gitter auf, und das
    /// wird angelegt, auch wo die Karte gar keine Zeilen dafür hat (der Rest ist Void).</summary>
    private const int MaxDimension = 1024;

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

        var colors = ParseColors(sourceName, caveFields);

        var letter = char.ToUpperInvariant(RequireField(caveFields, "Cave", sourceName)[0]);

        // Der Schwierigkeitsgrad steht im Cave-Kopf und bestimmt (zusammen mit Kind) allein das
        // Spieltempo — in BD1 gibt es kein Tempo pro Cave (siehe CaveSpeed).
        var level = RequireByte(caveFields, "Level", sourceName);
        if (level is < 1 or > 5)
        {
            throw new FormatException($"{sourceName}: Level muss 1..5 sein, gefunden: {level}.");
        }

        // Größe des Gitters, in dem die Cave liegt — sie selbst darf darin beliebig geformt sein.
        var width = RequireInt(caveFields, "Width", sourceName, 1, MaxDimension);
        var height = RequireInt(caveFields, "Height", sourceName, 1, MaxDimension);
        var tiles = ParseMap(sourceName, mapLines, width, height);
        ValidateEnclosure(sourceName, tiles, width, height);

        // Der Eingang legt die Startposition der Kamera fest (Camera.CenterOn beim Cave-Start) —
        // ohne ihn wäre die Cave unspielbar.
        if (Array.IndexOf(tiles, (byte)Element.Entrance) < 0)
        {
            throw new FormatException($"{sourceName}: [Map] enthält keinen Eingang ('P').");
        }

        // Ohne Ausgang lässt sich die Cave nicht verlassen. In BD1 liegt er in der Hälfte der Caves
        // in der Randmauer (Cave E: Spalte 39, Cave H: Spalte 0) — er ist dort als Stahlwand getarnt
        // und erst am Blinken zu erkennen.
        if (Array.IndexOf(tiles, (byte)Element.EscapeDoor) < 0)
        {
            throw new FormatException($"{sourceName}: [Map] enthält keinen Ausgang ('X').");
        }

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
    /// Liest die Farbfelder Color1-Color4 des [Cave]-Abschnitts: je ein RGB-Wert (#RRGGBB). Color1
    /// ist die Farbe des Palettenindex 0, Color4 die des Index 3 — damit werden die Sprites der Cave
    /// eingefärbt (siehe Palette).
    /// </summary>
    private static Rgb[] ParseColors(string sourceName, Dictionary<string, string> caveFields)
    {
        var colors = new Rgb[PaletteSize];
        for (var i = 0; i < PaletteSize; i++)
        {
            var key = $"Color{i + 1}";
            var token = RequireField(caveFields, key, sourceName);
            if (!Rgb.TryParse(token, out colors[i]))
            {
                throw new FormatException($"{sourceName}: '{key}' erwartet einen RGB-Wert im Format #RRGGBB, gefunden: '{token}'.");
            }
        }

        return colors;
    }

    /// <summary>
    /// Liest das ASCII-Raster des [Map]-Abschnitts. Die Karte muss das Gitter nicht ausfüllen: Eine
    /// Zeile darf kürzer sein als Width, und es dürfen Zeilen fehlen — was die Karte nicht nennt,
    /// liegt außerhalb der Cave und wird Void (siehe VoidObject). So schreibt man nur die Höhle selbst
    /// hin, in welcher Form auch immer. Zu LANG darf eine Zeile dagegen nicht sein, und zu viele
    /// Zeilen dürfen es auch nicht sein — das wäre ein Tippfehler, kein Cave-Umriss.
    ///
    /// Eine Zeile aus lauter Void mitten in der Karte braucht mindestens ein '_': Leerzeilen überliest
    /// der Parser (sie trennen die Abschnitte).
    /// </summary>
    private static byte[] ParseMap(string sourceName, List<(string Line, int LineNumber)> mapLines, int width, int height)
    {
        if (mapLines.Count > height)
        {
            throw new FormatException(
                $"{sourceName}: [Map] hat {mapLines.Count} Kartenzeilen, erlaubt sind höchstens {height} (Height).");
        }

        var tiles = new byte[width * height];
        Array.Fill(tiles, (byte)Element.Void);

        for (var y = 0; y < mapLines.Count; y++)
        {
            var (line, lineNumber) = mapLines[y];
            if (line.Length > width)
            {
                throw new FormatException(
                    $"{sourceName}:{lineNumber}: Kartenzeile ist {line.Length} Zeichen lang, erlaubt sind höchstens {width} (Width).");
            }

            for (var x = 0; x < line.Length; x++)
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

    /// <summary>
    /// Besteht darauf, dass die Cave lückenlos von Stahl umschlossen ist.
    ///
    /// Das ist die Bedingung, unter der die Physik ohne jede Bereichsprüfung auskommt: Ein Objekt
    /// greift ungeprüft auf seine Nachbarn zu (CaveObject.Explode liest 3x3, FallingObject liest die
    /// Kachel unter sich), und nur weil kein Objekt je aus der Höhle herauskommt, läuft dabei kein
    /// Index aus dem Gitter. Bisher garantierte das die rechteckige Randmauer jeder BD1-Cave; seit die
    /// Cave beliebig geformt sein darf, muss der Stahlring geprüft werden, statt vorausgesetzt zu
    /// werden — sonst wäre ein Loch in der Mauer kein Cave-Fehler, sondern ein Absturz.
    ///
    /// Geprüft per Flutfüllung von der Gitterkante her, quer durch alles, was nicht Stahl ist (der
    /// Ausgang zählt dazu: Er sitzt in der Hälfte der BD1-Caves in der Randmauer, sieht aus wie Stahl
    /// und hält wie Stahl). Was die Flut erreicht, ist außerhalb der Cave und muss Void sein — ist es
    /// etwas anderes, führt von draußen ein Weg hinein. Und umgekehrt: Was Void ist, muss die Flut auch
    /// erreichen, sonst wäre es ein Nichts INNERHALB der Höhle — dort gehört ein Leerraum (' ') hin.
    /// </summary>
    private static void ValidateEnclosure(string sourceName, byte[] tiles, int width, int height)
    {
        var outside = new bool[tiles.Length];
        var queue = new Queue<int>();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                {
                    Flood(sourceName, tiles, width, outside, queue, x, y);
                }
            }
        }

        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            var (x, y) = (index % width, index / width);

            if (x > 0)
            {
                Flood(sourceName, tiles, width, outside, queue, x - 1, y);
            }

            if (x < width - 1)
            {
                Flood(sourceName, tiles, width, outside, queue, x + 1, y);
            }

            if (y > 0)
            {
                Flood(sourceName, tiles, width, outside, queue, x, y - 1);
            }

            if (y < height - 1)
            {
                Flood(sourceName, tiles, width, outside, queue, x, y + 1);
            }
        }

        for (var index = 0; index < tiles.Length; index++)
        {
            if ((Element)(tiles[index] & CaveObjects.ElementMask) == Element.Void && !outside[index])
            {
                throw new FormatException(
                    $"{sourceName}: Kachel ({index % width},{index / width}) ist Void ('_'), liegt aber INNERHALB "
                    + "der Stahlmauer — dort gehört ein Leerraum (' ') hin.");
            }
        }
    }

    /// <summary>Ein Schritt der Flutfüllung: Am Stahl (und am Ausgang darin) endet sie; alles andere,
    /// was sie von draußen erreicht, gehört nicht zur Cave und muss deshalb Void sein.</summary>
    private static void Flood(string sourceName, byte[] tiles, int width, bool[] outside, Queue<int> queue, int x, int y)
    {
        var index = (y * width) + x;
        if (outside[index])
        {
            return;
        }

        var element = (Element)(tiles[index] & CaveObjects.ElementMask);
        if (element is Element.TitaniumWall or Element.EscapeDoor)
        {
            return;
        }

        if (element != Element.Void)
        {
            throw new FormatException(
                $"{sourceName}: Die Cave ist nicht lückenlos von Stahl ('W') umschlossen — Kachel ({x},{y}) trägt "
                + $"{element}, ist aber von außerhalb der Cave erreichbar.");
        }

        outside[index] = true;
        queue.Enqueue(index);
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
