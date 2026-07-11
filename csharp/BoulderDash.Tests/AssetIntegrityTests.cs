namespace BoulderDash.Tests;

/// <summary>
/// Stellt sicher, dass die nach csharp/BoulderDash.Game/Assets kopierte Original-Binärdatei
/// (nur noch DEMO.BIN — Caves und Sprites liegen als Textdateien vor) nicht von der
/// Archivdatei in src/ abweicht (reiner Lesevergleich, src/ wird nie verändert).
/// </summary>
public class AssetIntegrityTests
{
    [Theory]
    [InlineData("DEMO.BIN")]
    public void Kopie_ist_byteidentisch_zum_Original(string dateiname)
    {
        var kopie = File.ReadAllBytes(Path.Combine(TestPaths.GameAssets, dateiname));
        var original = File.ReadAllBytes(Path.Combine(TestPaths.SrcRoot, dateiname));

        Assert.Equal(original, kopie);
    }
}
