using BoulderDash.Core.Objects;
using BoulderDash.Core.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BoulderDash.Game.Graphics;

/// <summary>
/// Zeichnet den laufenden Spielzustand: das Kachel-Sichtfenster ab der aktuellen Kameraposition
/// (Original 20x12, per Spielflächen-Zoom bis zur vollen Cave, siehe ViewportSize) und darüber die
/// Verdeckung (ScreenCover) — entspricht copy64()+BildschirmMaske() aus src/BOULDER.CPP.
///
/// Welches Bild eine Kachel zeigt, entscheidet das Objekt selbst (CaveObject.Appearance); der
/// Renderer fragt nur noch danach und führt aus. Die Frameauswahl liegt damit in Core und ist
/// headless prüfbar — früher steckte sie als switch über alle Elemente hier drin.
/// </summary>
public sealed class CaveRenderer
{
    public const int TileSize = 16;

    /// <summary>Höhe der Statuszeile in Pixeln (eine BIOS-Textzeile); das Spielfeld beginnt darunter.</summary>
    public const int StatusLineHeight = 8;

    /// <summary>Womit eine verdeckte Kachel überzeichnet wird — der durchlaufende Rand-Füllstein.
    /// Er liegt ÜBER dem Gitter statt darin und gehört deshalb zu keiner Höhle (Cave.Nowhere);
    /// der Renderer hält ihn selbst und taktet ihn mit der Cave mit.</summary>
    private readonly BorderFillObject _cover = new(Cave.Nowhere);

    /// <summary>Was an der Stelle einer Kreatur steht, die im Nebel vergessen ist
    /// (CaveObject.VisibleInFog): Leerraum — genau das, worüber Geist und Schmetterling ziehen.
    /// Gehört wie die Verdeckung zu keiner Höhle.</summary>
    private readonly EmptyObject _forgotten = new(Cave.Nowhere);

    private readonly SpriteAtlas _atlas;

    public CaveRenderer(SpriteAtlas atlas)
    {
        _atlas = atlas;
    }

    /// <summary>Logische Größe der Zeichenfläche für ein Sichtfenster: Spielfeld plus Statuszeile.
    /// Beim Original-Sichtfenster 20x12 ergibt das genau die VGA-Auflösung 320x200.</summary>
    public static (int Width, int Height) LogicalSize(ViewportSize viewport) =>
        (viewport.Columns * TileSize, StatusLineHeight + (viewport.Rows * TileSize));

    public void Draw(SpriteBatch batch, Cave cave, Camera camera, GameState state, InputState input, Clocks clocks, ScreenCover? cover, ExploreMap? explore = null)
    {
        var viewport = camera.Viewport;
        var context = new RenderContext(clocks.Clk4, state.ExitFlashOn, state.EnchantedWallRunning, input);

        // Die Verdeckung läuft im selben Takt wie die Objekte, gehört aber nicht zum Gitter.
        _cover.AnimationPhase = cave.AnimationPhase;

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
                // bis zum vollständigen Zudecken. Sie hat Vorrang vor dem Nebel — sonst wäre die
                // Stahlwand-Animation über unerkundetem Gelände nicht zu sehen.
                if (cover is not null && cover.IsCovered(x, y))
                {
                    _atlas.Draw(batch, destination, _cover.Appearance(context));
                    continue;
                }

                // Cave-Explore (siehe ExploreMap): Unerkundetes wird schlicht nicht gezeichnet — das
                // RenderTarget ist schwarz gelöscht (BoulderDashGame.Draw), es braucht kein schwarzes
                // Sprite. Erkundetes außerhalb des Blickradius kommt im Nebelgrau.
                var visibility = explore?.Visibility(x, y) ?? TileVisibility.Visible;
                if (visibility == TileVisibility.Hidden)
                {
                    continue;
                }

                var fogged = visibility == TileVisibility.Explored;

                // Der Nebel zeigt nur die erinnerte Umgebung. Wer aus eigenem Antrieb umherzieht, ist
                // dort nicht zu sehen — an seiner Stelle steht der Leerraum, über den er zieht.
                var tile = cave.Get(x, y);
                if (fogged && !tile.VisibleInFog)
                {
                    tile = _forgotten;
                }

                _atlas.Draw(batch, destination, tile.Appearance(context), fogged);
            }
        }
    }
}
