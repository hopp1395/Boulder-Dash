namespace BoulderDash.Core.Data;

/// <summary>
/// Parser für SPRITES.BIN. 49 Einträge: 48× 16x16 Pixel (256 Byte, 1 Byte/Pixel =
/// Palettenindex 0-3) und ein abschließender 16x24-Eintrag (384 Byte) für die
/// "Stahl-lauf"-Gleitanimation (MASK_SLAUF). Nach jedem Eintrag folgen 2 Bytes, die im
/// Original nur überlesen werden (Load_Sprites, src/INTRO.CPP:133-151) und keinen
/// definierten Inhalt haben.
///
/// Die Größe des letzten Eintrags (384 statt der zunächst vermuteten 512 Byte) wurde durch
/// exaktes Nachrechnen der realen Dateigröße verifiziert: 12770 = 48*(256+2) + (384+2).
/// </summary>
public static class SpriteFile
{
    public const int SpriteWidth = 16;
    public const int NormalSpriteHeight = 16;
    public const int LastSpriteHeight = 24;
    public const int SpriteCount = 49;
    public const int LastSpriteIndex = SpriteCount - 1;

    private const int SeparatorSize = 2;

    /// <summary>Rohe Pixeldaten je Eintrag (Palettenindex 0-3 je Byte, zeilenweise, 16 Pixel breit).</summary>
    public static IReadOnlyList<byte[]> LoadAll(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var result = new List<byte[]>(SpriteCount);
        var offset = 0;

        for (var i = 0; i < SpriteCount; i++)
        {
            var size = i == LastSpriteIndex ? SpriteWidth * LastSpriteHeight : SpriteWidth * NormalSpriteHeight;
            var sprite = new byte[size];
            Array.Copy(bytes, offset, sprite, 0, size);
            result.Add(sprite);
            offset += size + SeparatorSize;
        }

        return result;
    }
}
