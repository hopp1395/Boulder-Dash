using BoulderDash.Core.Flow;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class DemoPlayerTests
{
    private const int CaveWidth = 40;

    [Fact]
    public void ApplyScancode_Rechts_setzt_Richtung_Flags_und_Blickrichtung()
    {
        var input = new InputState();

        DemoPlayer.ApplyScancode(0x4D, input, CaveWidth);

        Assert.Equal(1, input.Direction);
        Assert.Equal(0x40, input.Flags);
        Assert.Equal((byte)0, input.FacingLeft);
    }

    [Fact]
    public void ApplyScancode_Links_setzt_Richtung_Flags_und_Blickrichtung()
    {
        var input = new InputState();

        DemoPlayer.ApplyScancode(0x4B, input, CaveWidth);

        Assert.Equal(-1, input.Direction);
        Assert.Equal(0x10, input.Flags);
        Assert.Equal((byte)1, input.FacingLeft);
    }

    [Fact]
    public void ApplyScancode_Runter_und_Hoch_nutzen_die_Cavebreite()
    {
        var input = new InputState();

        DemoPlayer.ApplyScancode(0x50, input, CaveWidth);
        Assert.Equal(CaveWidth, input.Direction);

        DemoPlayer.ApplyScancode(0x48, input, CaveWidth);
        Assert.Equal(-CaveWidth, input.Direction);
    }

    [Fact]
    public void ApplyScancode_BreakCode_loescht_nur_sein_eigenes_Flag_und_stoppt_ueber_SettleIdleState()
    {
        var input = new InputState();
        DemoPlayer.ApplyScancode(0x4D, input, CaveWidth); // rechts halten
        Assert.Equal(0x40, input.Flags);

        DemoPlayer.ApplyScancode(0xCD, input, CaveWidth); // rechts loslassen

        Assert.Equal(0, input.Flags);
        Assert.Equal(0, input.Direction); // SettleIdleState: Flags<0x10 -> Direction=0
    }

    [Fact]
    public void ApplyScancode_NoOp_laesst_bestehenden_Zustand_unangetastet()
    {
        var input = new InputState();
        DemoPlayer.ApplyScancode(0x4D, input, CaveWidth);

        DemoPlayer.ApplyScancode(0x30, input, CaveWidth);

        Assert.Equal(1, input.Direction);
        Assert.Equal(0x40, input.Flags);
    }

    [Fact]
    public void ApplyScancode_Steuerungstaste_setzt_und_loescht_GrabModifier()
    {
        var input = new InputState();

        DemoPlayer.ApplyScancode(0x1D, input, CaveWidth);
        Assert.Equal((byte)6, input.GrabModifier);

        DemoPlayer.ApplyScancode(0x9D, input, CaveWidth);
        Assert.Equal((byte)0, input.GrabModifier);
    }

    [Fact]
    public void ApplyCurrent_wendet_den_Scancode_am_Index_0_sofort_an()
    {
        var player = new DemoPlayer([0x4D, DemoPlayer.Terminator]);
        var input = new InputState();

        player.ApplyCurrent(input, CaveWidth);

        Assert.Equal(1, input.Direction);
    }

    [Fact]
    public void AdvanceIfDue_rueckt_genau_einmal_pro_Clk1_Periode_vor()
    {
        // Clk1 hat Periode 3 (Clocks.cs): Post-Tick-Werte laufen 1,2,0,1,2,0,... — AdvanceIfDue
        // darf daher erst beim DRITTEN Tick auf den nächsten Scancode vorrücken.
        var player = new DemoPlayer([0x4D, 0x48, DemoPlayer.Terminator]);
        var input = new InputState();
        var clocks = new Clocks();

        player.ApplyCurrent(input, CaveWidth);
        Assert.Equal(1, input.Direction);

        clocks.Tick();
        player.AdvanceIfDue(clocks, input, CaveWidth);
        Assert.Equal(1, input.Direction);

        clocks.Tick();
        player.AdvanceIfDue(clocks, input, CaveWidth);
        Assert.Equal(1, input.Direction);

        clocks.Tick();
        player.AdvanceIfDue(clocks, input, CaveWidth);
        Assert.Equal(-CaveWidth, input.Direction);
    }

    [Fact]
    public void AdvanceIfDue_bleibt_am_Terminator_stehen_ohne_Fehler()
    {
        var player = new DemoPlayer([DemoPlayer.Terminator]);
        var input = new InputState();
        var clocks = new Clocks();

        Assert.True(player.IsAtEnd);

        for (var i = 0; i < 10; i++)
        {
            clocks.Tick();
            player.AdvanceIfDue(clocks, input, CaveWidth);
        }

        Assert.True(player.IsAtEnd);
    }
}
