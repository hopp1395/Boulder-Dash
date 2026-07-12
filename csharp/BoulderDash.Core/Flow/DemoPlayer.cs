using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Flow;

/// <summary>
/// Treibt die Demo-Wiedergabe (src/BOULDER.CPP: demo(), :337-378) — wendet die Züge aus der
/// Demo-Textdatei (siehe DemoTextFile) auf ein InputState an. Ein Zug hält genau eine Richtung für
/// eine clk_1-Periode; das wiederholte Anwenden desselben Zugs pro Periode ist äquivalent zum
/// Gedrückthalten der Taste. Wait löst alle Richtungen.
///
/// Timing wie beim Original-Scancode-Format: das Original rückt den Bytezeiger im
/// Foreground-Loop über ein pr-Latch genau einmal pro clk_1-Periode vor — hier nachgebildet
/// über <see cref="ApplyCurrent"/> (einmalig beim Demo-Start) und <see cref="AdvanceIfDue"/>
/// (einmal pro Tick, direkt nach dem physiktragenden GameTick.Tick()-Aufruf).
/// </summary>
public sealed class DemoPlayer
{
    private readonly IReadOnlyList<DemoStep> _steps;
    private int _index;

    public DemoPlayer(IReadOnlyList<DemoStep> steps)
    {
        _steps = steps;
    }

    public bool IsAtEnd => _index >= _steps.Count;

    /// <summary>Wendet den Zug am aktuellen Index an, ohne vorzurücken — entspricht dem
    /// ersten, noch nicht taktgebundenen Mov_Rockford-Aufruf beim Betreten der Demo-Schleife.</summary>
    public void ApplyCurrent(InputState input, int caveWidth)
    {
        if (!IsAtEnd)
        {
            ApplyStep(_steps[_index], input, caveWidth);
        }
    }

    /// <summary>Einmal pro Tick, nach dem GameTick.Tick()-Aufruf, aufrufen: rückt bei
    /// Clk1==0 (Periodenende, siehe Clocks) auf den nächsten Zug vor und wendet ihn an.
    /// Am Ende der Aufzeichnung angekommen, bleibt der Index stehen.</summary>
    public void AdvanceIfDue(Clocks clocks, InputState input, int caveWidth)
    {
        if (clocks.Clk1 != 0 || IsAtEnd)
        {
            return;
        }

        _index++;
        if (!IsAtEnd)
        {
            ApplyStep(_steps[_index], input, caveWidth);
        }
    }

    public static void ApplyStep(DemoStep step, InputState input, int caveWidth)
    {
        // Ein Demo-Zug hält genau EINE Richtung. InputState führt inzwischen eine echte Menge
        // gehaltener Tasten (statt wie das DOS-Original nur die zuletzt gedrückte zu merken) —
        // deshalb müssen die übrigen Richtungen hier ausdrücklich losgelassen werden. Die Demo
        // enthält ohnehin nie zwei Richtungen gleichzeitig, das Ergebnis bleibt also dasselbe.
        input.ReleaseRight();
        input.ReleaseLeft();
        input.ReleaseDown();
        input.ReleaseUp();

        switch (step)
        {
            case DemoStep.Right: input.PressRight(); break;
            case DemoStep.Left: input.PressLeft(); break;
            case DemoStep.Down: input.PressDown(caveWidth); break;
            case DemoStep.Up: input.PressUp(caveWidth); break;
            case DemoStep.Wait: break;
        }
    }
}
