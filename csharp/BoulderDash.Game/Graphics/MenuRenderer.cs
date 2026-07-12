using BoulderDash.Core.Flow;
using BoulderDash.Core.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BoulderDash.Game.Graphics;

/// <summary>
/// Hauptmenü, entspricht menuausgabe() + den Textausgaben in Start_menu (src/BOULDER.CPP:286,
/// 431-468). Der dekorative Rahmen ist dasselbe 20x12-Kachelmuster wie im Original (obere 7
/// Zeilen Mauer/Stahl-Rahmen, untere 5 Zeilen leer); die CP437-Rahmenzeichen ("Í», etc.) sind
/// durch einfache ASCII-Zeichen ersetzt, da BiosFont kein originalgetreuer CP437-Font ist.
/// </summary>
public sealed class MenuRenderer
{
    private const int TileSize = CaveRenderer.TileSize;

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

    public MenuRenderer(SpriteAtlas atlas, BiosFont font)
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

    public void Draw(SpriteBatch batch, GameSession session)
    {
        DrawBackground(batch);
        DrawTitleBox();
        _font.DrawText(batch, MakeCopyrightOrMarqueeLine(session), RowPosition(17), Color.White);
        _font.DrawText(batch, "F1 - SPIEL STARTEN  F2 - DEMO", RowPosition(19), Color.White);
        // F5 ist kein Original-Menüpunkt, sondern der Zugang zur Prüfstand-Cave (GameSession.TestCaveName).
        _font.DrawText(batch, "F3 - HILFE          F4 - ENDE  F5 - TEST", RowPosition(21), Color.White);
        _font.DrawText(batch, BuildSelectorLine(session), RowPosition(23), Color.White);

        void DrawTitleBox()
        {
            _font.DrawText(batch, "+-------------------+", RowPosition(5), Color.White);
            _font.DrawText(batch, "| BOULDER DASH V1.1 |", RowPosition(6), Color.White);
            _font.DrawText(batch, "+-------------------+", RowPosition(7), Color.White);
        }
    }

    /// <summary>Auswahlbildschirm des Testmodus (F5) — kein Original-Bildschirm, sondern der
    /// Entwicklerzugang zu den Prüfstand-Caves (GameSession.TestCaves). Jede Cave prüft genau eine
    /// Korrektur am Objektverhalten; die Anleitung dazu steht im Kopf der jeweiligen Cave-Datei.</summary>
    public void DrawTestMenu(SpriteBatch batch, GameSession session)
    {
        DrawBackground(batch);

        _font.DrawText(batch, "+-------------------+", RowPosition(5), Color.White);
        _font.DrawText(batch, "|     TESTMODUS     |", RowPosition(6), Color.White);
        _font.DrawText(batch, "+-------------------+", RowPosition(7), Color.White);

        for (var i = 0; i < GameSession.TestCaves.Count; i++)
        {
            var marker = i == session.TestCaveIndex ? ">" : " ";
            _font.DrawText(batch, $"{marker} {i + 1}  {GameSession.TestCaves[i].Title}", RowPosition(11 + i), Color.White);
        }

        _font.DrawText(batch, "HOCH/RUNTER ODER 1-5 WAEHLEN", RowPosition(19), Color.White);
        _font.DrawText(batch, "F1 - STARTEN        ESC - ZURUECK", RowPosition(21), Color.White);
    }

    private void DrawBackground(SpriteBatch batch)
    {
        for (var row = 0; row < CaveRenderer.ViewportRows; row++)
        {
            for (var col = 0; col < CaveRenderer.ViewportColumns; col++)
            {
                var element = BackgroundPattern[(row * CaveRenderer.ViewportColumns) + col];
                var (texture, source) = _atlas.GetDefaultSprite(element);
                var destination = new Rectangle(
                    col * TileSize, CaveRenderer.StatusLineHeight + (row * TileSize), TileSize, TileSize);
                batch.Draw(texture, destination, source, Color.White);
            }
        }
    }

    private static string MakeCopyrightOrMarqueeLine(GameSession session) => session.MarqueeVisibleText;

    private static string BuildSelectorLine(GameSession session) =>
        $"     LEVEL : {session.DifficultyLevel:D2}       CAVE : {session.SelectedCaveLetter}";

    private static Vector2 RowPosition(int textRow) => new(0, (textRow - 1) * BiosFont.GlyphSize);
}
