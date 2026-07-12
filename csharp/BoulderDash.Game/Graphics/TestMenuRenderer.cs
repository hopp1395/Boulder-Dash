using BoulderDash.Core.Flow;
using BoulderDash.Core.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BoulderDash.Game.Graphics;

/// <summary>
/// Auswahlbildschirm des Testmodus (F5) — kein Original-Bildschirm, sondern der Entwicklerzugang zu
/// den Prüfstand-Caves (GameSession.TestCaves). Ein leerer, schwarzer Schirm: nichts als die Liste,
/// ein Pfeil vor der gewählten Cave. Hoch/Runter wählt, Enter oder Leertaste startet, Escape geht
/// zurück — eine Legende dazu braucht es nicht, wer hier landet, kennt sie.
///
/// Früher trug er den Look des DOS-Menüs (menuausgabe(), src/BOULDER.CPP:431-468) mit Kachelrahmen,
/// Titelkasten und Tastenlegende; der Auswahlmarker '>' fiel darin gar nicht auf, weil der BiosFont
/// dieses Zeichen nicht kannte und es stillschweigend übersprang (jetzt hat er es).
/// </summary>
public sealed class TestMenuRenderer
{
    /// <summary>Linker Rand der Liste; der Pfeil steht davor, der Titel eine Spalte weiter.</summary>
    private const int Margin = BiosFont.GlyphSize;

    /// <summary>Zeilen, die auf den 320x200-Schirm passen (eine BIOS-Zeile ist 8 Pixel hoch).</summary>
    private const int ScreenRows = 200 / BiosFont.GlyphSize;

    // grundfarbe[]={0,1,6,2} in menuausgabe() (BOULDER.CPP:435-438) - die vier Farben der damaligen
    // 16-Farben-Tabelle als RGB-Werte (dunkel, weiß, blau, rot). Der Testmodus zeichnet selbst keine
    // Sprites mehr; die Palette bleibt der Menü-Kontext des SpriteAtlas (BoulderDashGame.SyncPalette).
    public static readonly Rgb[] MenuColors = [new(0x20, 0x20, 0x20), new(0xFF, 0xFF, 0xFF), new(0x20, 0x20, 0xBA), new(0xBA, 0x20, 0x20)];

    private readonly BiosFont _font;

    public TestMenuRenderer(BiosFont font)
    {
        _font = font;
    }

    /// <summary>Jede Prüfstand-Cave prüft genau eine Korrektur am Objektverhalten; die Anleitung dazu
    /// steht im Kopf der jeweiligen Cave-Datei. Die Liste steht senkrecht mittig und wächst mit jedem
    /// neuen Prüfstand nach oben und unten auseinander.</summary>
    public void Draw(SpriteBatch batch, GameSession session)
    {
        var firstRow = Math.Max(0, (ScreenRows - GameSession.TestCaves.Count) / 2);

        for (var i = 0; i < GameSession.TestCaves.Count; i++)
        {
            var marker = i == session.TestCaveIndex ? '>' : ' ';
            var position = new Vector2(Margin, (firstRow + i) * BiosFont.GlyphSize);
            _font.DrawText(batch, $"{marker} {GameSession.TestCaves[i].Title}", position, Color.White);
        }
    }
}
