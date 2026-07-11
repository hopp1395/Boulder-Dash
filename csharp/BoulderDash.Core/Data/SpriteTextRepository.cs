namespace BoulderDash.Core.Data;

/// <summary>
/// Lädt die 49 Rohsprites aus den Sprite-Textdateien eines Verzeichnisses (eine Datei pro
/// Objekt, Format siehe SpriteTextFile). Das Manifest legt Lade-Reihenfolge und erwartete
/// Frame-Zahl fest — die laufende Nummer über alle Frames ist der Rohsprite-Index, den
/// SpriteTables.FrameToRawSprite referenziert (ursprünglich die Reihenfolge in SPRITES.BIN,
/// Load_Sprites in src/INTRO.CPP:133-151).
/// </summary>
public sealed class SpriteTextRepository
{
    public const int SpriteWidth = 16;
    public const int NormalSpriteHeight = 16;
    public const int LastSpriteHeight = 24;
    public const int SpriteCount = 49;
    public const int LastSpriteIndex = SpriteCount - 1;

    /// <summary>
    /// Dateiname (ohne .txt, BDCFF-Objektname), erwartete Frame-Zahl und Frame-Höhe;
    /// die Reihenfolge ergibt die Rohsprite-Indizes (Summe der Frames = 49).
    /// "outbox" ist der Blinkframe der Ein-/Ausgangstür, "border-fill" das 16x24 hohe
    /// Gleit-Sprite des Randaufbaus (MASK_SLAUF).
    /// </summary>
    public static readonly IReadOnlyList<(string FileName, int FrameCount, int Height)> Manifest =
    [
        ("space", 1, NormalSpriteHeight),
        ("dirt", 1, NormalSpriteHeight),
        ("boulder", 1, NormalSpriteHeight),
        ("diamond", 8, NormalSpriteHeight),
        ("brick-wall", 1, NormalSpriteHeight),
        ("steel-wall", 1, NormalSpriteHeight),
        ("rockford", 11, NormalSpriteHeight),
        ("amoeba", 4, NormalSpriteHeight),
        ("firefly", 4, NormalSpriteHeight),
        ("butterfly", 3, NormalSpriteHeight),
        ("outbox", 1, NormalSpriteHeight),
        ("explosion", 3, NormalSpriteHeight),
        ("magic-wall", 4, NormalSpriteHeight),
        ("diamond-explosion", 5, NormalSpriteHeight),
        ("border-fill", 1, LastSpriteHeight),
    ];

    /// <summary>Rohe Pixeldaten je Sprite (Palettenindex 0-3 je Byte, zeilenweise, 16 Pixel breit).</summary>
    public IReadOnlyList<byte[]> RawSprites { get; }

    public SpriteTextRepository(string spritesDirectory)
    {
        var sprites = new List<byte[]>(SpriteCount);
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

            sprites.AddRange(data.Frames);
        }

        RawSprites = sprites;
    }
}
