using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class DissolveTests
{
    [Fact]
    public void Waehrend_der_ersten_Phase_ist_das_gesamte_Sichtfenster_verdeckt()
    {
        var dissolve = new Dissolve(new BorlandRandom());
        dissolve.Tick(1);

        for (var i = 0; i < 240; i++)
        {
            Assert.True(dissolve.IsCovered(i));
        }
    }

    [Fact]
    public void Zelle_0_wird_beim_ersten_echten_Tick_deterministisch_ohne_Zufallszahl_geraeumt()
    {
        // level_in() initialisiert j lokal mit 0 bei jedem Aufruf (BOULDER.CPP:571); beim ersten
        // Aufruf nach der Reset-Phase ist newmask[0] noch 15, die while-Bedingung also sofort
        // falsch, wodurch Zelle 0 ohne jeden RNG-Aufruf geräumt wird — ein Original-Determinismus,
        // unabhängig vom Zufallsgenerator-Seed.
        var dissolve = new Dissolve(new BorlandRandom());
        dissolve.Tick(4); // letzter Reset-Tick (anfang_var<5)
        dissolve.Tick(5); // erster Tick der Zufallsphase

        Assert.False(dissolve.IsCovered(0));
    }

    [Fact]
    public void Ueber_60_Ticks_werden_praktisch_alle_Zellen_aufgeloest()
    {
        // Pro Tick werden bis zu 4 Zellen geräumt (60 Ticks * 4 = 240 = Sichtfenstergröße), aber
        // durch die Original-Retry-Logik bei zufälligen Kollisionen ist keine 100%-Garantie
        // gegeben — der Renderer wendet die Maske ab anfang_var>=65 ohnehin nicht mehr an
        // (siehe CaveRenderer). Hier prüfen wir nur, dass der Fortschritt plausibel ist.
        var dissolve = new Dissolve(new BorlandRandom());

        for (byte entranceProgress = 1; entranceProgress <= 64; entranceProgress++)
        {
            dissolve.Tick(entranceProgress);
        }

        var nochVerdeckt = Enumerable.Range(0, 240).Count(dissolve.IsCovered);

        Assert.True(nochVerdeckt < 10, $"Noch {nochVerdeckt} von 240 Zellen verdeckt");
    }
}
