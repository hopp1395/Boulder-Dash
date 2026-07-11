using BoulderDash.Core.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BoulderDash.Game.Graphics;

/// <summary>
/// Zeichnet eine Cave statisch (ohne Animation/Simulation) in ihrem Ladezustand: 20x12-Kachel-
/// Sichtfenster ab Kamera-Startposition, entspricht copy64()+BildschirmMaske() aus
/// src/BOULDER.CPP:516-565,691-705 für den allerersten, noch nicht angelaufenen Frame.
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

    public void Draw(SpriteBatch batch, CaveData cave)
    {
        for (var row = 0; row < ViewportRows; row++)
        {
            var y = cave.CameraStartY + row;
            if (y >= cave.Height)
            {
                continue;
            }

            for (var col = 0; col < ViewportColumns; col++)
            {
                var x = cave.CameraStartX + col;
                if (x >= cave.Width)
                {
                    continue;
                }

                var element = cave.GetElement(x, y);
                var (texture, source) = _atlas.GetDefaultSprite(element);
                var destination = new Rectangle(col * TileSize, StatusLineHeight + (row * TileSize), TileSize, TileSize);
                batch.Draw(texture, destination, source, Color.White);
            }
        }
    }
}
