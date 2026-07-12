using BoulderDash.Core.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BoulderDash.Game.Graphics;

/// <summary>
/// Zeichnet den laufenden Spielzustand: das Kachel-Sichtfenster ab der aktuellen Kameraposition
/// (Original 20x12, per Spielflächen-Zoom bis zur vollen Cave, siehe ViewportSize), die
/// Verdeckungs-Überlagerung (ScreenCover), Ein-/Ausgangstür-Blinkanimation und die
/// Sprite-Animationszyklen (entspricht copy64()+BildschirmMaske() aus src/BOULDER.CPP, plus der
/// Frameauswahl aus sprites_wechsel()/boulder_lauf(), :593-646).
/// Die Rockford-Blickrichtung wird per SpriteEffects gespiegelt statt wie im Original die
/// Sprite-Pixel im Speicher zu vertauschen — beobachtbar identisch, technisch einfacher.
/// </summary>
public sealed class CaveRenderer
{
    public const int TileSize = 16;

    /// <summary>Höhe der Statuszeile in Pixeln (eine BIOS-Textzeile); das Spielfeld beginnt darunter.</summary>
    public const int StatusLineHeight = 8;

    private readonly SpriteAtlas _atlas;

    public CaveRenderer(SpriteAtlas atlas)
    {
        _atlas = atlas;
    }

    /// <summary>Logische Größe der Zeichenfläche für ein Sichtfenster: Spielfeld plus Statuszeile.
    /// Beim Original-Sichtfenster 20x12 ergibt das genau die VGA-Auflösung 320x200.</summary>
    public static (int Width, int Height) LogicalSize(ViewportSize viewport) =>
        (viewport.Columns * TileSize, StatusLineHeight + (viewport.Rows * TileSize));

    public void Draw(SpriteBatch batch, Cave cave, Camera camera, GameState state, InputState input, Clocks clocks, ScreenCover? cover)
    {
        var viewport = camera.Viewport;

        // Ist das Sichtfenster größer als die Cave (z. B. eine 20x12-Intermission bei großem Zoom),
        // steht die Kamera auf 0 (Camera.Clamp) und die Cave wird im schwarzen Rest zentriert.
        var offsetX = Math.Max(0, (viewport.Columns - cave.Width) * TileSize / 2);
        var offsetY = Math.Max(0, (viewport.Rows - cave.Height) * TileSize / 2);

        var rows = Math.Min(viewport.Rows, cave.Height);
        var columns = Math.Min(viewport.Columns, cave.Width);

        for (var row = 0; row < rows; row++)
        {
            var y = camera.Y + row;
            if (y >= cave.Height)
            {
                continue;
            }

            for (var col = 0; col < columns; col++)
            {
                var x = camera.X + col;
                if (x >= cave.Width)
                {
                    continue;
                }

                var destination = new Rectangle(
                    offsetX + (col * TileSize),
                    offsetY + StatusLineHeight + (row * TileSize),
                    TileSize,
                    TileSize);

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
                if (element == Element.Rockford)
                {
                    DrawRockford(batch, destination, state, input);
                    continue;
                }

                var (texture, source) = GetSpriteForElement(element, state, clocks);
                batch.Draw(texture, destination, source, Color.White);
            }
        }
    }

    /// <summary>
    /// Rockford: in Bewegung der Laufzyklus (boulder_lauf(), BOULDER.CPP:639-645), im Stand die
    /// Ruheanimation aus BD1 (BDCFF-Objektspezifikation 0006). Blinzeln und Fußtappen sind dort
    /// unabhängig voneinander, weil der C64 obere und untere Körperhälfte getrennt steuert — das
    /// Bild wird deshalb auch hier aus zwei Hälften zusammengesetzt. Die Sprite-Frames sind genau
    /// dafür gemacht: 14/15 ändern ausschließlich die Augenpartie, 16/17 ausschließlich Arme und
    /// Füße, sonst sind alle vier deckungsgleich mit dem Ruheframe 13. Ob überhaupt geblinzelt
    /// bzw. getappt wird, würfelt der GameTick pro Sequenz aus.
    /// </summary>
    private void DrawRockford(SpriteBatch batch, Rectangle destination, GameState state, InputState input)
    {
        if (input.Direction != 0)
        {
            // Gespiegelt wird nur der Laufzyklus — auch das Original vertauscht die Sprite-Pixel
            // ausschließlich für die Frames 18-23 (boulder_lauf(), BOULDER.CPP:620-635), die
            // nach vorn gerichteten Ruheframes bleiben ungespiegelt.
            var (texture, source) = _atlas.GetFrameSprite(18 + state.WechselBoulder);
            var effects = input.FacingLeft == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            batch.Draw(texture, destination, source, Color.White, 0f, Vector2.Zero, effects, 0f);
            return;
        }

        DrawHalf(batch, destination, EyeFrame(state), top: true);
        DrawHalf(batch, destination, FootFrame(state), top: false);
    }

    /// <summary>Augenpartie der Ruheanimation: Frame 13 offen, 14 halb, 15 geschlossen. Ein
    /// Blinzeln schließt die Augen einmal in der Mitte der 8-Frame-Sequenz und öffnet sie wieder.</summary>
    private static int EyeFrame(GameState state) => state.RockfordBlinking
        ? state.WechselVier switch
        {
            2 or 5 => 14,
            3 or 4 => 15,
            _ => 13,
        }
        : 13;

    /// <summary>Fußpartie der Ruheanimation: Frame 13 im Stillstand, sonst der Tappzyklus aus
    /// Frame 16 (Fuß unten) und 17 (Fuß angehoben) — zwei Schläge je Sequenz.</summary>
    private static int FootFrame(GameState state) => state.RockfordTapping
        ? (state.WechselVier / 2) % 2 == 0 ? 16 : 17
        : 13;

    /// <summary>Zeichnet die obere oder untere Hälfte eines 16x16-Frames an ihren Platz in der Kachel.</summary>
    private void DrawHalf(SpriteBatch batch, Rectangle destination, int frame, bool top)
    {
        const int halfHeight = TileSize / 2;
        var offset = top ? 0 : halfHeight;
        var (texture, source) = _atlas.GetFrameSprite(frame);

        batch.Draw(
            texture,
            new Rectangle(destination.X, destination.Y + offset, TileSize, halfHeight),
            new Rectangle(source.X, source.Y + offset, TileSize, halfHeight),
            Color.White);
    }

    /// <summary>Frameauswahl wie sprites_wechsel()/boulder_lauf() (BOULDER.CPP:593-646).
    /// Rockford fehlt hier bewusst — er wird in DrawRockford gezeichnet.</summary>
    private (Texture2D Texture, Rectangle Source) GetSpriteForElement(Element element, GameState state, Clocks clocks)
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
            case Element.BorderFill:
                return _atlas.GetBorderFillSprite(state.WechselVier);
            default:
                return _atlas.GetDefaultSprite(element);
        }
    }
}
