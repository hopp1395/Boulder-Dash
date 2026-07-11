using BoulderDash.Core.Data;

namespace BoulderDash.Tests;

public class SpriteTextRepositoryTests
{
    private static readonly string SpritesPath = Path.Combine(TestPaths.GameAssets, "Sprites");

    [Fact]
    public void Repository_liefert_49_Sprites_mit_korrekten_Groessen()
    {
        var sprites = new SpriteTextRepository(SpritesPath).RawSprites;

        Assert.Equal(SpriteTextRepository.SpriteCount, sprites.Count);

        for (var i = 0; i < sprites.Count; i++)
        {
            var erwarteteGroesse = i == SpriteTextRepository.LastSpriteIndex
                ? SpriteTextRepository.SpriteWidth * SpriteTextRepository.LastSpriteHeight
                : SpriteTextRepository.SpriteWidth * SpriteTextRepository.NormalSpriteHeight;

            Assert.Equal(erwarteteGroesse, sprites[i].Length);
        }
    }

    [Fact]
    public void Alle_Pixelwerte_sind_gueltige_Palettenindizes_0_bis_3()
    {
        var sprites = new SpriteTextRepository(SpritesPath).RawSprites;

        foreach (var sprite in sprites)
        {
            Assert.All(sprite, pixel => Assert.InRange(pixel, (byte)0, (byte)3));
        }
    }
}
