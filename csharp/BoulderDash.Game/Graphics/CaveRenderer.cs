using BoulderDash.Core.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BoulderDash.Game.Graphics;

/// <summary>
/// Zeichnet den laufenden Spielzustand: 20x12-Kachel-Sichtfenster ab der aktuellen
/// Kameraposition, Verdeckungs-Überlagerung (ScreenCover), Ein-/Ausgangstür-Blinkanimation und die
/// Sprite-Animationszyklen (entspricht copy64()+BildschirmMaske() aus src/BOULDER.CPP, plus der
/// Frameauswahl aus sprites_wechsel()/boulder_lauf(), :593-646).
/// Die Rockford-Blickrichtung wird per SpriteEffects gespiegelt statt wie im Original die
/// Sprite-Pixel im Speicher zu vertauschen — beobachtbar identisch, technisch einfacher.
/// </summary>
public sealed class CaveRenderer
{
    public const int TileSize = 16;
    public const int ViewportColumns = 20;
    public const int ViewportRows = 12;

    /// <summary>Höhe der Statuszeile in Pixeln (eine BIOS-Textzeile); das Spielfeld beginnt darunter.</summary>
    public const int StatusLineHeight = 8;

    private readonly SpriteAtlas _atlas;

    public CaveRenderer(SpriteAtlas atlas)
    {
        _atlas = atlas;
    }

    public void Draw(SpriteBatch batch, Cave cave, Camera camera, GameState state, InputState input, Clocks clocks, ScreenCover? cover)
    {
        for (var row = 0; row < ViewportRows; row++)
        {
            var y = camera.Y + row;
            if (y >= cave.Height)
            {
                continue;
            }

            for (var col = 0; col < ViewportColumns; col++)
            {
                var x = camera.X + col;
                if (x >= cave.Width)
                {
                    continue;
                }

                var destination = new Rectangle(col * TileSize, StatusLineHeight + (row * TileSize), TileSize, TileSize);

                // Die Verdeckungsmaske liegt in Cave-Koordinaten (siehe ScreenCover) und entscheidet
                // selbst, wie lange sie gilt: beim Cave-Start bis zum Vollaufdecken, am Cave-Ende
                // bis zum vollständigen Zudecken.
                if (cover is not null && cover.IsCovered(x, y))
                {
                    var (coveredTexture, coveredSource) = _atlas.GetBorderFillSprite(state.WechselVier);
                    batch.Draw(coveredTexture, destination, coveredSource, Color.White);
                    continue;
                }

                var element = cave.GetElement(x, y);
                var (texture, source) = GetSpriteForElement(element, state, clocks, input);
                var effects = element == Element.Rockford && input.FacingLeft == 1
                    ? SpriteEffects.FlipHorizontally
                    : SpriteEffects.None;
                batch.Draw(texture, destination, source, Color.White, 0f, Vector2.Zero, effects, 0f);
            }
        }
    }

    /// <summary>Frameauswahl wie sprites_wechsel()/boulder_lauf() (BOULDER.CPP:593-646).</summary>
    private (Texture2D Texture, Rectangle Source) GetSpriteForElement(Element element, GameState state, Clocks clocks, InputState input)
    {
        switch (element)
        {
            case Element.Entrance:
                // Blinkt zwischen Frame 48/49, solange Rockford noch nicht erschienen ist.
                return _atlas.GetFrameSprite(clocks.Clk4 < 3 ? 49 : 48);
            case Element.EscapeDoor:
                // Sieht wie Stahl aus, bis die Diamantenquote erreicht ist — danach blinkt er offen.
                return state.ExitFlashOn
                    ? _atlas.GetFrameSprite(clocks.Clk4 < 3 ? 49 : 48)
                    : _atlas.GetDefaultSprite(Element.EscapeDoor);
            case Element.Jewel:
                return _atlas.GetFrameSprite(3 + state.WechselVier);
            case Element.Amoeba:
                return _atlas.GetFrameSprite(24 + state.WechselVier);
            case Element.Firefly:
                return _atlas.GetFrameSprite(32 + state.WechselVier);
            case Element.Butterfly:
                return _atlas.GetFrameSprite(40 + state.WechselVier);
            case Element.Explosion:
                return _atlas.GetFrameSprite(52 + state.WechselExplo);
            case Element.JewelExplosion:
                return _atlas.GetFrameSprite(68 + state.WechselExplo);
            case Element.EnchantedWall:
                return state.EnchantedWallRunning
                    ? _atlas.GetFrameSprite(60 + state.WechselVier)
                    : _atlas.GetFrameSprite(11);
            case Element.Rockford:
                return input.Direction != 0
                    ? _atlas.GetFrameSprite(18 + state.WechselBoulder)
                    : _atlas.GetFrameSprite(13);
            case Element.BorderFill:
                return _atlas.GetBorderFillSprite(state.WechselVier);
            default:
                return _atlas.GetDefaultSprite(element);
        }
    }
}
