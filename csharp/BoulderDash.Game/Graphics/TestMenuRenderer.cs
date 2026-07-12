using BoulderDash.Core.Flow;
using BoulderDash.Core.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BoulderDash.Game.Graphics;

/// <summary>
/// Auswahlbildschirm des Testmodus (F5) — kein Original-Bildschirm, sondern der Entwicklerzugang
/// zu den Prüfstand-Caves (GameSession.TestCaves). Er behält den Look des früheren DOS-Menüs
/// (menuausgabe(), src/BOULDER.CPP:431-468): dasselbe 20x12-Kachelmuster als Rahmen und die
/// DOS-Menüpalette. Das eigentliche Hauptmenü ist inzwischen der BD1-Titel-/Option-Screen
/// (TitleRenderer, BD1-Ausnahme — siehe CLAUDE.md).
/// </summary>
public sealed class TestMenuRenderer
{
    private const int TileSize = CaveRenderer.TileSize;

    // Der Testmodus behält den DOS-Look und damit das feste 20x12-Kachelraster (320x200) — der
    // Spielflächen-Zoom des Spielfelds gilt hier nicht.
    private const int MenuColumns = 20;
    private const int MenuRows = 12;

    // menu[] aus menuausgabe() (BOULDER.CPP:441-452): 4=Mauer, 5=Stahl, 0=Leer.
    private static readonly Element[] BackgroundPattern = ParsePattern(
        "44444444444444444444" +
        "45555555555555555554" +
        "45555555555555555554" +
        "45555555555555555554" +
        "45555555555555555554" +
        "45555555555555555554" +
        "44444444444444444444" +
        "00000000000000000000" +
        "00000000000000000000" +
        "00000000000000000000" +
        "00000000000000000000" +
        "00000000000000000000");

    // grundfarbe[]={0,1,6,2} in menuausgabe() (BOULDER.CPP:435-438).
    public static readonly byte[] MenuBaseColors = [0, 1, 6, 2];

    private readonly SpriteAtlas _atlas;
    private readonly BiosFont _font;

    public TestMenuRenderer(SpriteAtlas atlas, BiosFont font)
    {
        _atlas = atlas;
        _font = font;
    }

    private static Element[] ParsePattern(string digits)
    {
        var result = new Element[digits.Length];
        for (var i = 0; i < digits.Length; i++)
        {
            result[i] = (Element)(digits[i] - '0');
        }

        return result;
    }

    /// <summary>Jede Prüfstand-Cave prüft genau eine Korrektur am Objektverhalten; die Anleitung
    /// dazu steht im Kopf der jeweiligen Cave-Datei.</summary>
    public void Draw(SpriteBatch batch, GameSession session)
    {
        DrawBackground(batch);

        _font.DrawText(batch, "+-------------------+", RowPosition(5), Color.White);
        _font.DrawText(batch, "|     TESTMODUS     |", RowPosition(6), Color.White);
        _font.DrawText(batch, "+-------------------+", RowPosition(7), Color.White);

        // Die Liste wächst mit jeder geprüften Korrektur: sie endet immer auf Zeile 18 und wächst nach
        // oben, bleibt dabei aber unterhalb des Titelkastens (Zeilen 5-7).
        var firstRow = Math.Max(8, 19 - GameSession.TestCaves.Count);
        for (var i = 0; i < GameSession.TestCaves.Count; i++)
        {
            var marker = i == session.TestCaveIndex ? ">" : " ";
            _font.DrawText(batch, $"{marker} {i + 1}  {GameSession.TestCaves[i].Title}", RowPosition(firstRow + i), Color.White);
        }

        _font.DrawText(batch, "HOCH/RUNTER ODER ZIFFER WAEHLEN", RowPosition(20), Color.White);
        _font.DrawText(batch, "F1 - STARTEN        ESC - ZURUECK", RowPosition(22), Color.White);
    }

    private void DrawBackground(SpriteBatch batch)
    {
        for (var row = 0; row < MenuRows; row++)
        {
            for (var col = 0; col < MenuColumns; col++)
            {
                var element = BackgroundPattern[(row * MenuColumns) + col];
                var (texture, source) = _atlas.GetDefaultSprite(element);
                var destination = new Rectangle(
                    col * TileSize, CaveRenderer.StatusLineHeight + (row * TileSize), TileSize, TileSize);
                batch.Draw(texture, destination, source, Color.White);
            }
        }
    }

    private static Vector2 RowPosition(int textRow) => new(0, (textRow - 1) * BiosFont.GlyphSize);
}
