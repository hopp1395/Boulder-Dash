using BoulderDash.Core.Data;
using BoulderDash.Core.Objects;
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

    /// <summary>Liefert Textur + 16x16-Quellrechteck für einen z_zeiger-Frame-Index. Der
    /// Zeilenversatz ist das Gleitfenster im 24 Zeilen hohen Rand-Sprite (siehe BorderFillObject);
    /// bei allen anderen Sprites ist er 0.</summary>
    public (Texture2D Texture, Rectangle Source) GetFrameSprite(int zFrameIndex, int rowOffset = 0)
    {
        var rawIndex = SpriteTables.FrameToRawSprite[zFrameIndex];
        var texture = _textures[rawIndex];
        var source = new Rectangle(0, rowOffset, texture.Width, CaveRenderer.TileSize);
        return (texture, source);
    }

    /// <summary>Standard-(Anfangs-)Frame eines Elements — das Bild, das ein frisch geladenes,
    /// noch nicht angelaufenes Spiel zeigt (CaveObject.DefaultFrame).</summary>
    public (Texture2D Texture, Rectangle Source) GetDefaultSprite(Element element) =>
        GetFrameSprite(CaveObjects.Prototype(element).DefaultFrame);

    /// <summary>Führt den Zeichenauftrag eines Cave-Objekts aus (siehe TileAppearance): eine ganze
    /// Kachel — oder zwei Hälften, wenn das Objekt sein Bild aus zweien zusammensetzt.</summary>
    public void Draw(SpriteBatch batch, Rectangle destination, in TileAppearance appearance)
    {
        if (appearance.BottomFrame is { } bottomFrame)
        {
            DrawHalf(batch, destination, appearance.Frame, top: true);
            DrawHalf(batch, destination, bottomFrame, top: false);
            return;
        }

        var (texture, source) = GetFrameSprite(appearance.Frame, appearance.RowOffset);
        var effects = appearance.FlipHorizontally ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        batch.Draw(texture, destination, source, Color.White, 0f, Vector2.Zero, effects, 0f);
    }

    /// <summary>Zeichnet die obere oder untere Hälfte eines Frames an ihren Platz in der Kachel.</summary>
    private void DrawHalf(SpriteBatch batch, Rectangle destination, int frame, bool top)
    {
        const int halfHeight = CaveRenderer.TileSize / 2;
        var offset = top ? 0 : halfHeight;
        var (texture, source) = GetFrameSprite(frame);

        batch.Draw(
            texture,
            new Rectangle(destination.X, destination.Y + offset, CaveRenderer.TileSize, halfHeight),
            new Rectangle(source.X, source.Y + offset, CaveRenderer.TileSize, halfHeight),
            Color.White);
    }
}
