using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class BorlandRandomTests
{
    [Fact]
    public void Werte_liegen_immer_im_Bereich_0_bis_32767()
    {
        var random = new BorlandRandom();

        for (var i = 0; i < 10_000; i++)
        {
            var value = random.Next();
            Assert.InRange(value, 0, 32767);
        }
    }

    [Fact]
    public void Gleicher_Seed_liefert_identische_Sequenz()
    {
        var a = new BorlandRandom(1);
        var b = new BorlandRandom(1);

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(a.Next(), b.Next());
        }
    }

    [Fact]
    public void Verschiedene_Seeds_liefern_unterschiedliche_erste_Werte()
    {
        var a = new BorlandRandom(1);
        var b = new BorlandRandom(2);

        Assert.NotEqual(a.Next(), b.Next());
    }
}
