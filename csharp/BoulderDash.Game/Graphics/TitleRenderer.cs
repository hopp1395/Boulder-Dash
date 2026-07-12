using BoulderDash.Core.Data;
using BoulderDash.Core.Flow;
using BoulderDash.Core.Simulation;
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
/// Das Mauermuster im Hintergrund ist wie im Original animiert: Es läuft vertikal durch,
/// mit demselben Gleitfenster-Mechanismus wie die Zudeck-Stahlwand (siehe BorderFillObject:
/// ein 16-Zeilen-Fenster, das in 8 Phasen durch ein 24 Zeilen hohes Sprite wandert). Die
/// beiden Referenz-Screenshots belegen das —
/// ihr Muster unterscheidet sich exakt um eine vertikale Phasenverschiebung des 8x8-Rasters.
/// Beim Laden wird das Muster aus dem Titelbild extrahiert, jede reine Muster-Zeichenzelle
/// der Assets transparent gemacht und dahinter das durchlaufende Muster gezeichnet; Zellen
/// mit Logo-/Text-Inhalt bleiben als Standbild stehen (im Original animiert nur das
/// Muster-Zeichen selbst, nicht die Logo-Zeichen).
///
/// Die Textzeilen des Option-Screens kommen aus dem BiosFont (8x8) statt aus dem doppelt
/// breiten C64-Font des Originals (16px/Zeichen) — die Zeilen sitzen deshalb zentriert auf
/// den Original-Zeilenpositionen, sind aber schmaler als im Original.
/// </summary>
public sealed class TitleRenderer
{
    /// <summary>Das Hintergrundmuster ist ein 8x8-Zeichenraster (C64-Chars) und in beide
    /// Richtungen 8-periodisch — zugleich die Zellgröße der Muster-Erkennung und die
    /// Phasenzahl der Animation (wie CaveObject.AnimationPhase 0-7).</summary>
    private const int PatternSize = 8;

    private const int ScreenWidth = 320;

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
    private readonly Texture2D _pattern;
    private readonly BiosFont _font;

    /// <summary>Takt der Muster-Animation: Im Spiel rückt die Animationsphase einen Schritt pro
    /// Tick weiter — auf dem Titel gibt es kein Cave-Tempo, deshalb dient die Grad-1-Tickrate
    /// (50 ms) als Referenztakt.</summary>
    private readonly double _secondsPerPhase = CaveSpeed.For(1, isIntermission: false).SecondsPerTick;

    public TitleRenderer(GraphicsDevice device, string screensDirectory, BiosFont font)
    {
        _font = font;

        var title = Load(Path.Combine(screensDirectory, "title.txt"));
        var optionLogo = Load(Path.Combine(screensDirectory, "option-logo.txt"));

        var tile = ExtractPatternTile(title);
        _pattern = BuildPatternTexture(device, tile);
        _title = BuildOverlayTexture(device, title, tile);
        _optionLogo = BuildOverlayTexture(device, optionLogo, tile);
    }

    private static SpriteData Load(string path) =>
        SpriteTextFile.Parse(File.ReadAllText(path), Path.GetFileName(path));

    /// <summary>Zelle (1,1) des Titelbilds ist reines Muster (Spalte/Zeile 0 belegt der weiße
    /// Rahmen) — von dort kommt das 8x8-Muster für Erkennung und Animation.
    /// ScreenAssetTests sichert diese Annahme ab.</summary>
    private static byte[] ExtractPatternTile(SpriteData title)
    {
        var tile = new byte[PatternSize * PatternSize];
        for (var y = 0; y < PatternSize; y++)
        {
            for (var x = 0; x < PatternSize; x++)
            {
                tile[(y * PatternSize) + x] = title.Frames[0][((y + PatternSize) * title.Width) + x + PatternSize];
            }
        }

        return tile;
    }

    /// <summary>Das Muster flächig gekachelt, um eine Periode höher als der Bildschirm —
    /// das Gleitfenster (siehe DrawAnimatedPattern) verschiebt darin nur den Quellausschnitt,
    /// wie SpriteAtlas.GetBorderFillSprite im 24 Zeilen hohen Border-Fill-Sprite.</summary>
    private static Texture2D BuildPatternTexture(GraphicsDevice device, byte[] tile)
    {
        const int height = 200 + PatternSize;
        var pixels = new Color[ScreenWidth * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < ScreenWidth; x++)
            {
                pixels[(y * ScreenWidth) + x] = ScreenColors[tile[((y % PatternSize) * PatternSize) + (x % PatternSize)]];
            }
        }

        var texture = new Texture2D(device, ScreenWidth, height);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>Baut die Overlay-Textur eines Screens: Zeichenzellen, die (in irgendeiner
    /// vertikalen Phase) exakt das Hintergrundmuster zeigen, werden transparent — dort scheint
    /// die Animation durch. Alle übrigen Zellen (Logo, Rahmen, First-Star-Band) bleiben deckend.
    /// Die Erkennung arbeitet zellweise, weil das Original zeichenbasiert ist: eine Zelle ist
    /// entweder ganz Muster-Zeichen oder ganz Logo-Zeichen, Mischformen gibt es nicht.</summary>
    private static Texture2D BuildOverlayTexture(GraphicsDevice device, SpriteData data, byte[] tile)
    {
        var frame = data.Frames[0];
        var pixels = new Color[data.Width * data.Height];
        for (var i = 0; i < frame.Length; i++)
        {
            pixels[i] = ScreenColors[frame[i]];
        }

        for (var cellY = 0; cellY + PatternSize <= data.Height; cellY += PatternSize)
        {
            for (var cellX = 0; cellX + PatternSize <= data.Width; cellX += PatternSize)
            {
                if (!IsPatternCell(data, tile, cellX, cellY))
                {
                    continue;
                }

                for (var y = 0; y < PatternSize; y++)
                {
                    for (var x = 0; x < PatternSize; x++)
                    {
                        pixels[((cellY + y) * data.Width) + cellX + x] = Color.Transparent;
                    }
                }
            }
        }

        var texture = new Texture2D(device, data.Width, data.Height);
        texture.SetData(pixels);
        return texture;
    }

    private static bool IsPatternCell(SpriteData data, byte[] tile, int cellX, int cellY)
    {
        for (var shift = 0; shift < PatternSize; shift++)
        {
            var matches = true;
            for (var y = 0; y < PatternSize && matches; y++)
            {
                for (var x = 0; x < PatternSize; x++)
                {
                    var pixel = data.Frames[0][((cellY + y) * data.Width) + cellX + x];
                    if (pixel != tile[(((y + shift) % PatternSize) * PatternSize) + x])
                    {
                        matches = false;
                        break;
                    }
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reiner Titelbildschirm: Logo plus First-Star-Schriftzug, ein 320x200-Vollbild.</summary>
    public void DrawTitle(SpriteBatch batch, double totalSeconds)
    {
        DrawAnimatedPattern(batch, totalSeconds, _title.Height);
        batch.Draw(_title, Vector2.Zero, Color.White);
    }

    /// <summary>Option-Screen: Logo oben (Zeilen 0-151 inkl. der weißen Trennbalken), darunter
    /// die fünf Textzeilen des Originals auf den Original-Pixelzeilen 152-191. Die im Original
    /// blau abgesetzten Werte (die Einsen, Cave-Buchstabe, Levelziffer) bleiben blau.</summary>
    public void DrawOptionScreen(SpriteBatch batch, GameSession session, double totalSeconds)
    {
        DrawAnimatedPattern(batch, totalSeconds, _optionLogo.Height);
        batch.Draw(_optionLogo, Vector2.Zero, Color.White);

        DrawCentered(batch, 152, ("BY PETER LIEPA WITH CHRIS GREY 1984", Color.White));
        DrawCentered(batch, 160, ("PC VERSION BY JAN HOPPE 1999, 2026", Color.White));
        DrawCentered(batch, 168, ("PRESS BUTTON TO PLAY", Color.White));
        DrawCentered(batch, 176,
            ("1", Highlight), (" PLAYER  ", Color.White), ("1", Highlight), (" JOYSTICK", Color.White));
        DrawCentered(batch, 184,
            ("CAVE: ", Color.White), (session.SelectedCaveLetter.ToString(), Highlight),
            ("  LEVEL: ", Color.White), (session.DifficultyLevel.ToString(), Highlight));
    }

    /// <summary>Das durchlaufende Mauermuster hinter den transparenten Zellen des Overlays:
    /// Der Quellausschnitt wandert mit wachsender Phase nach unten durch die Mustertextur,
    /// das Muster scheint dadurch nach oben zu laufen — dieselbe Richtung und derselbe
    /// 8-Phasen-Zyklus wie bei der Zudeck-Stahlwand (siehe BorderFillObject).</summary>
    private void DrawAnimatedPattern(SpriteBatch batch, double totalSeconds, int height)
    {
        var phase = (int)(totalSeconds / _secondsPerPhase) % PatternSize;
        batch.Draw(
            _pattern,
            new Rectangle(0, 0, ScreenWidth, height),
            new Rectangle(0, phase, ScreenWidth, height),
            Color.White);
    }

    private void DrawCentered(SpriteBatch batch, int y, params (string Text, Color Color)[] segments)
    {
        var totalLength = segments.Sum(segment => segment.Text.Length);
        var x = (ScreenWidth - (totalLength * BiosFont.GlyphSize)) / 2;
        foreach (var (text, color) in segments)
        {
            _font.DrawText(batch, text, new Vector2(x, y), color);
            x += text.Length * BiosFont.GlyphSize;
        }
    }
}
