using BoulderDash.Core.Data;

namespace BoulderDash.Tests;

/// <summary>
/// Gegenprobe für die BD1-Titelgrafiken (Assets/Screens/): sie sind aus Referenz-Screenshots
/// quantisierte Vollbilder im Sprite-Textformat und müssen exakt die Maße haben, mit denen der
/// TitleRenderer sie auf das 320x200-RenderTarget legt.
/// </summary>
public class ScreenAssetTests
{
    [Theory]
    [InlineData("title.txt", "Title", 320, 200)]
    [InlineData("option-logo.txt", "OptionLogo", 320, 152)]
    public void Titelgrafik_hat_die_erwarteten_Masse(string file, string name, int width, int height)
    {
        var path = Path.Combine(TestPaths.GameAssets, "Screens", file);
        var data = SpriteTextFile.Parse(File.ReadAllText(path), file);

        Assert.Equal(name, data.Name);
        Assert.Equal(width, data.Width);
        Assert.Equal(height, data.Height);
        Assert.Single(data.Frames);
        Assert.Equal(width * height, data.Frames[0].Length);
    }

    /// <summary>Der TitleRenderer extrahiert das animierte Hintergrundmuster aus Zelle (8,8)
    /// des Titelbilds und kachelt es 8x8-periodisch — beide Annahmen müssen im Asset stimmen,
    /// sonst läuft die Muster-Animation gegen falsche Pixel.</summary>
    [Fact]
    public void Titelbild_traegt_bei_Zelle_8_8_das_8x8_periodische_Hintergrundmuster()
    {
        var path = Path.Combine(TestPaths.GameAssets, "Screens", "title.txt");
        var data = SpriteTextFile.Parse(File.ReadAllText(path), "title.txt");
        var frame = data.Frames[0];

        byte At(int x, int y) => frame[(y * data.Width) + x];

        for (var y = 8; y < 16; y++)
        {
            for (var x = 8; x < 16; x++)
            {
                Assert.Equal(At(x, y), At(x + 8, y)); // horizontal periodisch (Nachbarzelle rechts)
                Assert.Equal(At(x, y), At(x, y + 8)); // vertikal periodisch (Nachbarzelle unten)
            }
        }

        // Kein degeneriertes Muster (z. B. einfarbige Fläche) — die Zelle muss mehrere Farben tragen.
        var distinct = new HashSet<byte>();
        for (var y = 8; y < 16; y++)
        {
            for (var x = 8; x < 16; x++)
            {
                distinct.Add(At(x, y));
            }
        }

        Assert.True(distinct.Count >= 3, "Musterzelle (8,8) sieht nicht nach dem Mauermuster aus.");
    }
}
