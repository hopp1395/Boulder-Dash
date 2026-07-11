using BoulderDash.Core.Data;
using BoulderDash.Core.Flow;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

/// <summary>
/// Spielt die Original-Demo (DEMO.BIN, Cave A) headless bis zum Ende durch und friert einen
/// Hash über alle durchlaufenen Grid-Zustände sowie die Endwerte ein — ein Regressionsschutz für
/// Physik (CavePhysics), Timing (GameTick/Clocks) und RNG (BorlandRandom) gemeinsam. Schlägt
/// dieser Test nach einer absichtlichen Änderung fehl, die eingefrorenen Werte neu ermitteln
/// (Testausgabe zeigt den tatsächlich berechneten Hash) und hier eintragen.
/// </summary>
public class GoldenStateTests
{
    // DEMO.BIN wurde für die 1999er-Cave-A (LEVEL.BIN-Layout) aufgezeichnet, nicht für die
    // seit der Umstellung auf das BD1-Rohformat geladene Original-BD1-Cave-A - die Eingaben
    // desyncen daher zwangsläufig (bewusste Folge des Cave-Tauschs). Neu einfrieren, sobald eine
    // zur neuen Cave-A passende Demo-Aufzeichnung vorliegt.
    [Fact(Skip = "DEMO.BIN passt nicht mehr zur BD1-Cave-A-Geometrie - neu aufzeichnen und Werte neu einfrieren.")]
    public void Demo_spielt_Cave_A_deterministisch_durch_und_kehrt_ins_Menue_zurueck()
    {
        var caves = new CaveTextRepository(Path.Combine(TestPaths.GameAssets, "Caves"));
        var demoScancodes = DemoFile.Load(Path.Combine(TestPaths.GameAssets, "DEMO.BIN"));
        var session = new GameSession(caves, demoScancodes);

        session.MenuDemo();
        Assert.Equal(SessionPhase.DemoWait, session.Phase);

        const double step = 1.0 / 60.0;
        const int maxSteps = 60 * 60; // 60s Sicherheitsnetz — die Demo läuft real nur wenige Sekunden.

        var hash = 2166136261u; // FNV-1a Offset-Basis
        var steps = 0;
        while (session.Phase != SessionPhase.Menu && steps < maxSteps)
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

        Assert.True(steps < maxSteps, "Demo hat nicht innerhalb des Sicherheitsnetzes ins Menü zurückgefunden.");
        Assert.Equal(SessionPhase.Menu, session.Phase);

        // Eingefroren aus dem ersten erfolgreichen Lauf (siehe Klassenkommentar).
        Assert.Equal(2791, steps);
        Assert.Equal(2596374973u, hash);
        Assert.Equal(430, session.State.Score);
        Assert.Equal(19, session.State.JewelsCollected);
        Assert.Equal(0, session.State.Stat); // Rockford stirbt in der Demo nicht — Cave A wird erfolgreich beendet.
    }
}
