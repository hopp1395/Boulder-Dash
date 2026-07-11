namespace BoulderDash.Core.Data;

/// <summary>
/// ISpriteRepository-Implementierung für das Sprite-Textformat: lädt die Sprite-Objekte aus den
/// Textdateien eines Verzeichnisses (eine Datei pro Objekt, Format siehe SpriteTextFile).
/// Das Manifest legt Lade-Reihenfolge, Frame-Zahl und Frame-Höhe fest — die laufende Nummer über
/// alle Frames ist der Rohsprite-Index.
/// </summary>
public sealed class SpriteTextRepository : ISpriteRepository
{
    /// <summary>Alle Sprites sind 16 Pixel breit und (bis auf border-fill) 16 Pixel hoch.</summary>
    private const int SpriteWidth = 16;
    private const int NormalHeight = 16;

    /// <summary>border-fill (MASK_SLAUF) ist das 16x24 hohe Gleit-Sprite des Randaufbaus.</summary>
    private const int BorderFillHeight = 24;

    /// <summary>
    /// Dateiname (ohne .txt, BDCFF-Objektname), erwartete Frame-Zahl und Frame-Höhe;
    /// die Reihenfolge ergibt die Rohsprite-Indizes (Summe der Frames = 49).
    /// "outbox" ist der Blinkframe der Ein-/Ausgangstür.
    /// </summary>
    public static readonly IReadOnlyList<(string FileName, int FrameCount, int Height)> Manifest =
    [
        ("space", 1, NormalHeight),
        ("dirt", 1, NormalHeight),
        ("boulder", 1, NormalHeight),
        ("diamond", 8, NormalHeight),
        ("brick-wall", 1, NormalHeight),
        ("steel-wall", 1, NormalHeight),
        ("rockford", 11, NormalHeight),
        ("amoeba", 4, NormalHeight),
        ("firefly", 4, NormalHeight),
        ("butterfly", 3, NormalHeight),
        ("outbox", 1, NormalHeight),
        ("explosion", 3, NormalHeight),
        ("magic-wall", 4, NormalHeight),
        ("diamond-explosion", 5, NormalHeight),
        ("border-fill", 1, BorderFillHeight),
    ];

    private readonly IReadOnlyList<SpriteData> _sprites;

    public SpriteTextRepository(string spritesDirectory)
    {
        var sprites = new List<SpriteData>(Manifest.Count);
        foreach (var (fileName, frameCount, height) in Manifest)
        {
            var path = Path.Combine(spritesDirectory, fileName + ".txt");
            var data = SpriteTextFile.Parse(File.ReadAllText(path), path);
            if (data.Width != SpriteWidth || data.Height != height || data.Frames.Count != frameCount)
            {
                throw new FormatException(
                    $"{path}: erwartet werden {frameCount} Frames à {SpriteWidth}x{height}, " +
                    $"gefunden {data.Frames.Count} Frames à {data.Width}x{data.Height}.");
            }

            sprites.Add(data);
        }

        _sprites = sprites;
    }

    public IEnumerable<SpriteData> Get() => _sprites;
}
