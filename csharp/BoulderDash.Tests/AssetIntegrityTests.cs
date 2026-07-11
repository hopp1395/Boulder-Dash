namespace BoulderDash.Tests;

/// <summary>
/// Stellt sicher, dass die nach csharp/BoulderDash.Game/Assets kopierten Original-Binärdateien
/// nicht von den Archivdateien in src/ abweichen (reiner Lesevergleich, src/ wird nie verändert).
/// </summary>
public class AssetIntegrityTests
{
    [Theory]
    [InlineData("LEVEL.BIN")]
    [InlineData("SPRITES.BIN")]
    [InlineData("DEMO.BIN")]
    public void Kopie_ist_byteidentisch_zum_Original(string dateiname)
    {
        var kopie = File.ReadAllBytes(Path.Combine(TestPaths.GameAssets, dateiname));
        var original = File.ReadAllBytes(Path.Combine(TestPaths.SrcRoot, dateiname));

        Assert.Equal(original, kopie);
    }
}
