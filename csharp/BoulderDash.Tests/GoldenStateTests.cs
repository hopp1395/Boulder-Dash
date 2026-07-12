using BoulderDash.Core.Data;
using BoulderDash.Core.Flow;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

/// <summary>
/// Spielt die Demo (Assets/demo.txt, Cave A) headless bis zum Ende durch und friert einen
/// Hash über alle durchlaufenen Grid-Zustände sowie die Endwerte ein — ein Regressionsschutz für
/// Physik (die Regeln in den Cave-Objekten), Timing (GameTick/Clocks) und RNG (seed-festes
/// System.Random) gemeinsam. Schlägt
/// dieser Test nach einer absichtlichen Änderung fehl, die eingefrorenen Werte neu ermitteln
/// (Testausgabe zeigt den tatsächlich berechneten Hash) und hier eintragen.
/// </summary>
public class GoldenStateTests
{
    // Achtung: Die Demo läuft NICHT bis zum Cave-Ende — sie sammelt einen einzigen Diamanten und
    // endet dann mit ihrer Aufzeichnung (die BD1-Demo ist eine reine Vorführung, keine Lösung).
    // Cave A enthält außerdem weder Kreaturen noch Amoeba noch Zaubermauer. Dieser Test sichert
    // deshalb nur Steine/Erde/Diamanten, Timing und Session-Ablauf ab; die übrigen Objekte deckt
    // GoldenCaveScanTests ab.
    [Fact]
    public void Demo_spielt_Cave_A_deterministisch_durch_und_kehrt_zum_Titelbildschirm_zurueck()
    {
        var caves = new CaveTextRepository(Path.Combine(TestPaths.GameAssets, "Caves"));
        var demoSteps = DemoTextFile.Load(Path.Combine(TestPaths.GameAssets, "demo.txt"));
        var session = new GameSession(caves, demoSteps);

        session.StartDemo();
        Assert.Equal(SessionPhase.DemoPlaying, session.Phase);

        const double step = 1.0 / 60.0;
        const int maxSteps = 60 * 60; // 60s Sicherheitsnetz — die Demo läuft real nur wenige Sekunden.

        var hash = 2166136261u; // FNV-1a Offset-Basis
        var steps = 0;
        while (session.Phase != SessionPhase.TitleScreen && steps < maxSteps)
        {
            session.Update(step);
            steps++;

            if (session.Cave is { } cave)
            {
                for (var i = 0; i < cave.Width * cave.Height; i++)
                {
                    hash ^= cave.GetRaw(i);
                    hash *= 16777619u;
                }
            }
        }

        Assert.True(steps < maxSteps, "Demo hat nicht innerhalb des Sicherheitsnetzes zum Titelbildschirm zurückgefunden.");
        Assert.Equal(SessionPhase.TitleScreen, session.Phase);

        // Eingefroren aus dem Lauf mit der BD1-Demo auf der BD1-Cave-A.
        Assert.Equal(2569, steps);
        Assert.Equal(1788533998u, hash);
        Assert.Equal(10, session.State.Score);
        Assert.Equal(1, session.State.JewelsCollected);
        Assert.Equal(0, session.State.Stat); // Rockford stirbt in der Demo nicht.
        Assert.Equal(0, session.State.Stat); // Rockford stirbt in der Demo nicht — Cave A wird erfolgreich beendet.
    }
}
