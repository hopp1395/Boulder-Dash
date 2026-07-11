using BoulderDash.Core.Data;

namespace BoulderDash.Tests;

public class SpriteFileTests
{
    private static readonly string SpritesBinPath = Path.Combine(TestPaths.GameAssets, "SPRITES.BIN");

    [Fact]
    public void LoadAll_liefert_49_Sprites_mit_korrekten_Groessen()
    {
        var sprites = SpriteFile.LoadAll(SpritesBinPath);

        Assert.Equal(SpriteFile.SpriteCount, sprites.Count);

        for (var i = 0; i < sprites.Count; i++)
        {
            var erwarteteGroesse = i == SpriteFile.LastSpriteIndex
                ? SpriteFile.SpriteWidth * SpriteFile.LastSpriteHeight
                : SpriteFile.SpriteWidth * SpriteFile.NormalSpriteHeight;

            Assert.Equal(erwarteteGroesse, sprites[i].Length);
        }
    }

    [Fact]
    public void Alle_Pixelwerte_sind_gueltige_Palettenindizes_0_bis_3()
    {
        var sprites = SpriteFile.LoadAll(SpritesBinPath);

        foreach (var sprite in sprites)
        {
            Assert.All(sprite, pixel => Assert.InRange(pixel, (byte)0, (byte)3));
        }
    }
}
