using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BoulderDash.Game.Graphics;

/// <summary>
/// Hält die 49 Rohsprites aus den Sprite-Textdateien als Texture2D. Die Pixelbytes sind
/// Palettenindizes (0-3); da jede Cave nur 4 aktive Farben nutzt (setnewpalette,
/// src/BOULDER.CPP:496-512), werden die Texturen bei jedem Palettenwechsel per SetData neu
/// eingefärbt statt pro Cave neue Texturen anzulegen — das entspricht dem Original, wo derselbe
/// Sprite-Speicher einfach unter wechselnden VGA-DAC-Registern angezeigt wird.
/// </summary>
public sealed class SpriteAtlas
{
    private readonly Texture2D[] _textures;
    private readonly IReadOnlyList<byte[]> _rawSprites;

    public SpriteAtlas(GraphicsDevice device, ISpriteRepository sprites)
    {
        // Die Frames aller Objekte hintereinander sind die Rohsprites, die FrameToRawSprite indiziert.
        var raw = new List<byte[]>();
        var textures = new List<Texture2D>();
        foreach (var sprite in sprites.Get())
        {
            foreach (var frame in sprite.Frames)
            {
                raw.Add(frame);
                textures.Add(new Texture2D(device, sprite.Width, sprite.Height));
            }
        }

        _rawSprites = raw;
        _textures = textures.ToArray();
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

    /// <summary>Liefert Textur + 16x16-Quellrechteck (Zeile 0) für einen z_zeiger-Frame-Index.</summary>
    public (Texture2D Texture, Rectangle Source) GetFrameSprite(int zFrameIndex)
    {
        var rawIndex = SpriteTables.FrameToRawSprite[zFrameIndex];
        var texture = _textures[rawIndex];
        var source = new Rectangle(0, 0, texture.Width, CaveRenderer.TileSize);
        return (texture, source);
    }

    /// <summary>Standard-(Anfangs-)Frame eines Elements, siehe SpriteTables.GetDefaultFrame.</summary>
    public (Texture2D Texture, Rectangle Source) GetDefaultSprite(Element element) =>
        GetFrameSprite(SpriteTables.GetDefaultFrame(element));

    /// <summary>Rand-Gleitfenster (MASK_SLAUF): 16-Zeilen-Fenster im 24 Zeilen hohen Sprite 48,
    /// verschoben um wechselVier Zeilen (0-7) — buffer[MASK_SLAUF]=z_zeiger[76]+wechsel_vier*16
    /// in BOULDER.CPP:604 ist Byte-Zeiger-Arithmetik auf den Rohsprite, kein z_zeiger-Frameindex.</summary>
    public (Texture2D Texture, Rectangle Source) GetBorderFillSprite(byte wechselVier)
    {
        var rawIndex = SpriteTables.FrameToRawSprite[SpriteTables.GetDefaultFrame(Element.BorderFill)];
        var texture = _textures[rawIndex];
        var source = new Rectangle(0, wechselVier, texture.Width, CaveRenderer.TileSize);
        return (texture, source);
    }
}
