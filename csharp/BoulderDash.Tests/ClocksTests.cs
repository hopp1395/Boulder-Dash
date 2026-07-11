using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class ClocksTests
{
    [Fact]
    public void Clk1_hat_Periode_3()
    {
        var clocks = new Clocks();
        byte[] erwartet = [0, 1, 2, 0, 1, 2, 0];

        var werte = new byte[erwartet.Length];
        for (var i = 0; i < erwartet.Length; i++)
        {
            werte[i] = clocks.Clk1;
            clocks.Tick();
        }

        Assert.Equal(erwartet, werte);
    }

    [Fact]
    public void Clk4_hat_Periode_6()
    {
        var clocks = new Clocks();
        byte[] erwartet = [0, 1, 2, 3, 4, 5, 0, 1];

        var werte = new byte[erwartet.Length];
        for (var i = 0; i < erwartet.Length; i++)
        {
            werte[i] = clocks.Clk4;
            clocks.Tick();
        }

        Assert.Equal(erwartet, werte);
    }

    [Fact]
    public void Clk18_hat_Periode_22()
    {
        var clocks = new Clocks();

        for (var i = 0; i < 21; i++)
        {
            clocks.Tick();
        }

        Assert.Equal(21, clocks.Clk18);
        clocks.Tick();
        Assert.Equal(0, clocks.Clk18);
    }
}
