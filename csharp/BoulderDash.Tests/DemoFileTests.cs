using BoulderDash.Core.Data;
using BoulderDash.Core.Flow;

namespace BoulderDash.Tests;

public class DemoFileTests
{
    private static readonly byte[] KnownScancodes =
    [
        0x30, 0x48, 0x4B, 0x4D, 0x50, 0xC8, 0xCB, 0xCD, 0xD0,
    ];

    [Fact]
    public void Load_liefert_genau_256_Bytes()
    {
        var scancodes = DemoFile.Load(Path.Combine(TestPaths.GameAssets, "DEMO.BIN"));

        Assert.Equal(256, scancodes.Length);
    }

    [Fact]
    public void Load_enthaelt_nur_bekannte_Scancodes_bis_zum_Terminator()
    {
        var scancodes = DemoFile.Load(Path.Combine(TestPaths.GameAssets, "DEMO.BIN"));

        var terminatorIndex = Array.IndexOf(scancodes, DemoPlayer.Terminator);
        Assert.True(terminatorIndex > 0, "Terminator 0x31 nicht gefunden.");

        for (var i = 0; i < terminatorIndex; i++)
        {
            Assert.Contains(scancodes[i], KnownScancodes);
        }
    }
}
