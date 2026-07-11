using BoulderDash.Core.Data;
using BoulderDash.Core.Flow;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class DemoPlayerTests
{
    private const int CaveWidth = 40;

    [Fact]
    public void ApplyStep_Rechts_setzt_Richtung_Flags_und_Blickrichtung()
    {
        var input = new InputState();

        DemoPlayer.ApplyStep(DemoStep.Right, input, CaveWidth);

        Assert.Equal(1, input.Direction);
        Assert.Equal(0x40, input.Flags);
        Assert.Equal((byte)0, input.FacingLeft);
    }

    [Fact]
    public void ApplyStep_Links_setzt_Richtung_Flags_und_Blickrichtung()
    {
        var input = new InputState();

        DemoPlayer.ApplyStep(DemoStep.Left, input, CaveWidth);

        Assert.Equal(-1, input.Direction);
        Assert.Equal(0x10, input.Flags);
        Assert.Equal((byte)1, input.FacingLeft);
    }

    [Fact]
    public void ApplyStep_Runter_und_Hoch_nutzen_die_Cavebreite()
    {
        var input = new InputState();

        DemoPlayer.ApplyStep(DemoStep.Down, input, CaveWidth);
        Assert.Equal(CaveWidth, input.Direction);

        DemoPlayer.ApplyStep(DemoStep.Up, input, CaveWidth);
        Assert.Equal(-CaveWidth, input.Direction);
    }

    [Fact]
    public void ApplyStep_Wait_loest_alle_Richtungen_und_stoppt_ueber_SettleIdleState()
    {
        var input = new InputState();
        DemoPlayer.ApplyStep(DemoStep.Right, input, CaveWidth); // rechts halten
        Assert.Equal(0x40, input.Flags);

        DemoPlayer.ApplyStep(DemoStep.Wait, input, CaveWidth);

        Assert.Equal(0, input.Flags);
        Assert.Equal(0, input.Direction); // SettleIdleState: Flags<0x10 -> Direction=0
    }

    [Fact]
    public void ApplyCurrent_wendet_den_Zug_am_Index_0_sofort_an()
    {
        var player = new DemoPlayer([DemoStep.Right]);
        var input = new InputState();

        player.ApplyCurrent(input, CaveWidth);

        Assert.Equal(1, input.Direction);
    }

    [Fact]
    public void AdvanceIfDue_rueckt_genau_einmal_pro_Clk1_Periode_vor()
    {
        // Clk1 hat Periode 3 (Clocks.cs): Post-Tick-Werte laufen 1,2,0,1,2,0,... — AdvanceIfDue
        // darf daher erst beim DRITTEN Tick auf den nächsten Zug vorrücken.
        var player = new DemoPlayer([DemoStep.Right, DemoStep.Up]);
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
    public void AdvanceIfDue_bleibt_am_Ende_der_Aufzeichnung_stehen_ohne_Fehler()
    {
        var player = new DemoPlayer([]);
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
