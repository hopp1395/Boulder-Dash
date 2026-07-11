using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BoulderDash.Game.Graphics;

/// <summary>
/// Hält die 49 Rohsprites aus SPRITES.BIN als Texture2D. Die Pixelbytes sind Palettenindizes
/// (0-3); da jede Cave nur 4 aktive Farben nutzt (setnewpalette, src/BOULDER.CPP:496-512),
/// werden die Texturen bei jedem Palettenwechsel per SetData neu eingefärbt statt pro Cave
/// neue Texturen anzulegen — das entspricht dem Original, wo derselbe Sprite-Speicher einfach
/// unter wechselnden VGA-DAC-Registern angezeigt wird.
/// </summary>
public sealed class SpriteAtlas
{
    private readonly Texture2D[] _textures;
    private readonly IReadOnlyList<byte[]> _rawSprites;

    public SpriteAtlas(GraphicsDevice device, string spritesBinPath)
    {
        _rawSprites = SpriteFile.LoadAll(spritesBinPath);
        _textures = new Texture2D[SpriteFile.SpriteCount];

        for (var i = 0; i < SpriteFile.SpriteCount; i++)
        {
            var height = i == SpriteFile.LastSpriteIndex ? SpriteFile.LastSpriteHeight : SpriteFile.NormalSpriteHeight;
            _textures[i] = new Texture2D(device, SpriteFile.SpriteWidth, height);
        }
    }

    /// <summary>Färbt alle Sprites mit der aktuellen 4-Farben-Cave-Palette neu ein.</summary>
    public void ApplyPalette(Rgb[] palette)
    {
        for (var i = 0; i < _textures.Length; i++)
        {
            var raw = _rawSprites[i];
            var pixels = new Color[raw.Length];
            for (var p = 0; p < raw.Length; p++)
            {
                var rgb = palette[raw[p]];
                pixels[p] = new Color(rgb.R, rgb.G, rgb.B);
            }

            _textures[i].SetData(pixels);
        }
    }

    public Texture2D GetRawTexture(int rawSpriteIndex) => _textures[rawSpriteIndex];

    /// <summary>Liefert die Textur für ein Element in seinem Standard-(Anfangs-)Frame plus die
    /// 16x16-Quellrechteck-Zeile 0 (relevant für den 24 Zeilen hohen Rand-Sprite Nr. 48).</summary>
    public (Texture2D Texture, Rectangle Source) GetDefaultSprite(Element element)
    {
        var frame = SpriteTables.GetDefaultFrame(element);
        var rawIndex = SpriteTables.FrameToRawSprite[frame];
        var texture = _textures[rawIndex];
        var source = new Rectangle(0, 0, SpriteFile.SpriteWidth, SpriteFile.NormalSpriteHeight);
        return (texture, source);
    }
}
