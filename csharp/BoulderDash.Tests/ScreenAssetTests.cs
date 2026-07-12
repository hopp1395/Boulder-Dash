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
}
