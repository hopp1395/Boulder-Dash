using System.Text.Json;
using BoulderDash.Core.Data;
using CaveConverter;

var cavesJsonPath = args.Length > 0 ? args[0] : @"W:\repos\Boulder-Dash-C64\extracted\caves\caves.json";
var outputDir = args.Length > 1 ? args[1] : @"W:\repos\Boulder-Dash\csharp\BoulderDash.Game\Assets\Caves";
var referenceDir = Path.GetDirectoryName(cavesJsonPath)!;

Directory.CreateDirectory(outputDir);

var caveNames = new Dictionary<char, (string Name, string Description)>
{
    ['A'] = ("Intro", "Pick up jewels and exit before time is up"),
    ['B'] = ("Rooms", "Pick up jewels, but you must move boulders"),
    ['C'] = ("Maze", "Pick up jewels. You must get every jewel to exit"),
    ['D'] = ("Butterflies", "Drop boulders on butterflies to create jewels"),
    ['E'] = ("Guards", "Jewels guarded by deadly fireflies"),
    ['F'] = ("Firefly dens", "Each firefly is guarding a jewel"),
    ['G'] = ("Amoeba", "Surround amoeba with boulders; pick up created jewels"),
    ['H'] = ("Enchanted wall", "Activate enchanted wall; create jewels"),
    ['I'] = ("Greed", "Get many jewels; many available"),
    ['J'] = ("Tracks", "Get jewels, avoid fireflies"),
    ['K'] = ("Crowd", "Move boulders around in tight spaces"),
    ['L'] = ("Walls", "Blast through walls; drop boulders on fireflies"),
    ['M'] = ("Apocalypse", "Bring butterflies and amoeba together"),
    ['N'] = ("Zigzag", "Transform butterflies into jewels"),
    ['O'] = ("Funnel", "Enchanted wall at tunnel bottom"),
    ['P'] = ("Enchanted boxes", "Blast into square rooms; tops are enchanted walls"),
    ['Q'] = ("Intermission 1", "Bonus level"),
    ['R'] = ("Intermission 2", "Bonus level"),
    ['S'] = ("Intermission 3", "Bonus level"),
    ['T'] = ("Intermission 4", "Bonus level"),
};

using var json = JsonDocument.Parse(File.ReadAllText(cavesJsonPath));

var written = 0;
var matched = 0;
var roundTripped = 0;
var mismatched = new List<string>();
var roundTripFailed = new List<string>();

foreach (var entry in json.RootElement.EnumerateArray())
{
    var letter = entry.GetProperty("cave").GetString()![0];
    var typ = entry.GetProperty("typ").GetString();
    var hex = entry.GetProperty("hex").GetString()!;
    var data = Convert.FromHexString(hex);

    var (name, description) = caveNames[letter];
    var isIntermission = typ == "Intermission";

    var levels = Bd1RawParser.ParseLevels(data, letter, name, description, isIntermission);
    var referenceMaps = ReferenceMaps.Load(Path.Combine(referenceDir, $"cave_{letter}.txt"));

    for (var levelIndex = 0; levelIndex < levels.Length; levelIndex++)
    {
        var def = levels[levelIndex];
        var caveData = CaveMapBuilder.Build(def);
        var renderedMap = CaveAsciiMap.Render(caveData);

        var reference = referenceMaps[levelIndex];
        if (MapsMatch(renderedMap, reference))
        {
            matched++;
        }
        else
        {
            mismatched.Add($"{letter}-{levelIndex + 1}");
        }

        var path = Path.Combine(outputDir, $"cave-{letter}-{def.Level}.txt");
        File.WriteAllText(path, CaveTextWriter.Write(def, renderedMap));
        written++;

        // Round-Trip: die eben geschriebene Datei zurücklesen und die Kacheln vergleichen - stellt
        // sicher, dass die Datei allein (ohne Rohdaten) exakt dieselbe Cave ergibt.
        var reparsed = CaveTextFile.Parse(File.ReadAllText(path), path);
        if (reparsed.Tiles.SequenceEqual(caveData.Tiles))
        {
            roundTripped++;
        }
        else
        {
            roundTripFailed.Add($"{letter}-{levelIndex + 1}");
        }
    }
}

Console.WriteLine($"{written} Dateien geschrieben -> {outputDir}");
Console.WriteLine($"{matched}/{written} Karten stimmen mit den Referenzkarten überein.");
Console.WriteLine($"{roundTripped}/{written} Dateien ergeben beim Zurücklesen exakt dieselben Kacheln.");
if (mismatched.Count > 0)
{
    Console.WriteLine("Abweichend zur Referenz: " + string.Join(", ", mismatched));
}

if (roundTripFailed.Count > 0)
{
    Console.WriteLine("Round-Trip fehlgeschlagen: " + string.Join(", ", roundTripFailed));
}

if (mismatched.Count > 0 || roundTripFailed.Count > 0)
{
    Environment.Exit(1);
}

// Die Referenzkarten (cave_*.txt) verwenden für einige Objekt-Code-Varianten (z.B. 0x32, eine
// Butterfly-Richtungsvariante) mangels Legenden-Eintrag ein Platzhalter-'?' statt des tatsächlich
// dekodierten Objekts - an diesen Stellen ist jedes eigene Zeichen zulässig (siehe README dort).
static bool MapsMatch(string[] rendered, string[] reference)
{
    if (rendered.Length != reference.Length)
    {
        return false;
    }

    for (var y = 0; y < rendered.Length; y++)
    {
        if (rendered[y].Length != reference[y].Length)
        {
            return false;
        }

        for (var x = 0; x < rendered[y].Length; x++)
        {
            if (reference[y][x] == '?')
            {
                continue;
            }

            if (rendered[y][x] != reference[y][x])
            {
                return false;
            }
        }
    }

    return true;
}
