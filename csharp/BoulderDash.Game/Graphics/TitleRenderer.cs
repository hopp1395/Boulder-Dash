using BoulderDash.Core.Data;
using BoulderDash.Core.Flow;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BoulderDash.Game.Graphics;

/// <summary>
/// Zeichnet den BD1-Titelbildschirm und den Option-Screen (ersetzt das DOS-Menü, BD1-Ausnahme
/// wie ScreenCover/Amoeba/CaveSpeed — siehe CLAUDE.md). Die beiden Logo-Grafiken liegen als
/// Sprite-Text-Assets unter Assets/Screens/ (aus Referenz-Screenshots des C64-Originals
/// quantisiert) und werden einmalig mit festen C64-Farben zu Texturen gebaut — bewusst NICHT
/// über SpriteAtlas/Palette, damit die Cave-Palettenlogik (SyncPalette) unberührt bleibt.
///
/// Die Textzeilen des Option-Screens kommen aus dem BiosFont (8x8) statt aus dem doppelt
/// breiten C64-Font des Originals (16px/Zeichen) — die Zeilen sitzen deshalb zentriert auf
/// den Original-Zeilenpositionen, sind aber schmaler als im Original.
/// </summary>
public sealed class TitleRenderer
{
    /// <summary>Die 4 Farben beider Screens, aus den Referenz-Screenshots gemessen
    /// (C64-Blau $3E31A2 und -Hellblau $7C70DA der Pepto-Palette): Indizes 0-3 der Assets.</summary>
    private static readonly Color[] ScreenColors =
    [
        new(0, 0, 0),
        new(62, 49, 162),
        new(124, 112, 218),
        new(255, 255, 255),
    ];

    private static readonly Color Highlight = ScreenColors[1]; // Blau der hervorgehobenen Werte.

    private readonly Texture2D _title;
    private readonly Texture2D _optionLogo;
    private readonly BiosFont _font;

    public TitleRenderer(GraphicsDevice device, string screensDirectory, BiosFont font)
    {
        _font = font;
        _title = BuildTexture(device, Path.Combine(screensDirectory, "title.txt"));
        _optionLogo = BuildTexture(device, Path.Combine(screensDirectory, "option-logo.txt"));
    }

    private static Texture2D BuildTexture(GraphicsDevice device, string path)
    {
        var data = SpriteTextFile.Parse(File.ReadAllText(path), Path.GetFileName(path));
        var pixels = new Color[data.Width * data.Height];
        var frame = data.Frames[0];
        for (var i = 0; i < frame.Length; i++)
        {
            pixels[i] = ScreenColors[frame[i]];
        }

        var texture = new Texture2D(device, data.Width, data.Height);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>Reiner Titelbildschirm: Logo plus First-Star-Schriftzug, ein 320x200-Vollbild.</summary>
    public void DrawTitle(SpriteBatch batch)
    {
        batch.Draw(_title, Vector2.Zero, Color.White);
    }

    /// <summary>Option-Screen: Logo oben (Zeilen 0-151 inkl. der weißen Trennbalken), darunter
    /// die fünf Textzeilen des Originals auf den Original-Pixelzeilen 152-191. Die im Original
    /// blau abgesetzten Werte (die Einsen, Cave-Buchstabe, Levelziffer) bleiben blau.</summary>
    public void DrawOptionScreen(SpriteBatch batch, GameSession session)
    {
        batch.Draw(_optionLogo, Vector2.Zero, Color.White);

        DrawCentered(batch, 152, ("BY PETER LIEPA", Color.White));
        DrawCentered(batch, 160, ("WITH CHRIS GREY", Color.White));
        DrawCentered(batch, 168, ("PRESS BUTTON TO PLAY", Color.White));
        DrawCentered(batch, 176,
            ("1", Highlight), (" PLAYER  ", Color.White), ("1", Highlight), (" JOYSTICK", Color.White));
        DrawCentered(batch, 184,
            ("CAVE: ", Color.White), (session.SelectedCaveLetter.ToString(), Highlight),
            ("  LEVEL: ", Color.White), (session.DifficultyLevel.ToString(), Highlight));
    }

    private void DrawCentered(SpriteBatch batch, int y, params (string Text, Color Color)[] segments)
    {
        var totalLength = segments.Sum(segment => segment.Text.Length);
        var x = (320 - (totalLength * BiosFont.GlyphSize)) / 2;
        foreach (var (text, color) in segments)
        {
            _font.DrawText(batch, text, new Vector2(x, y), color);
            x += text.Length * BiosFont.GlyphSize;
        }
    }
}
