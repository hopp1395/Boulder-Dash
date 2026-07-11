using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Flow;

/// <summary>
/// Treibt die Demo-Wiedergabe (src/BOULDER.CPP: demo(), :337-378) — wendet die aufgezeichneten
/// Scancodes aus DEMO.BIN über Mov_Rockford-Semantik (GAME.CPP:11-30) auf ein InputState an.
///
/// Timing: das Original rückt den Bytezeiger im Foreground-Loop über ein pr-Latch genau einmal
/// pro clk_1-Periode vor, während Mov_Rockford() im selben Aufruf noch mit dem ALTEN Byte
/// aufgerufen wird — das neue Byte wird erst auf der nächsten (praktisch sofortigen)
/// Foreground-Iteration wirksam. Da regel() selbst nur einmal pro clk_1-Periode (im ISR) läuft,
/// ist das Ergebnis: Byte N ist ab dem Zeitpunkt aktiv, an dem Byte N gelesen wurde, bis zur
/// nächsten Periode. Hier nachgebildet über <see cref="ApplyCurrent"/> (einmalig beim
/// Demo-Start) und <see cref="AdvanceIfDue"/> (einmal pro Tick, direkt nach dem physiktragenden
/// GameTick.Tick()-Aufruf).
/// </summary>
public sealed class DemoPlayer
{
    public const byte Terminator = 0x31;

    private readonly byte[] _scancodes;
    private int _index;

    public DemoPlayer(byte[] scancodes)
    {
        _scancodes = scancodes;
    }

    public bool IsAtEnd => _scancodes[_index] == Terminator;

    /// <summary>Wendet den Scancode am aktuellen Index an, ohne vorzurücken — entspricht dem
    /// ersten, noch nicht taktgebundenen Mov_Rockford-Aufruf beim Betreten der Demo-Schleife.</summary>
    public void ApplyCurrent(InputState input, int caveWidth) => ApplyScancode(_scancodes[_index], input, caveWidth);

    /// <summary>Einmal pro Tick, nach dem GameTick.Tick()-Aufruf, aufrufen: rückt bei
    /// Clk1==0 (Periodenende, siehe Clocks) auf den nächsten Scancode vor und wendet ihn an.
    /// Am Terminator angekommen, bleibt der Index stehen (kein weiteres Vorrücken).</summary>
    public void AdvanceIfDue(Clocks clocks, InputState input, int caveWidth)
    {
        if (clocks.Clk1 != 0 || IsAtEnd)
        {
            return;
        }

        _index++;
        ApplyScancode(_scancodes[_index], input, caveWidth);
    }

    /// <summary>Mov_Rockford-Transliteration für die in DEMO.BIN vorkommenden Scancodes. 0x1D/0x9D
    /// (Strg/kop) kommen in der Original-Demo nicht vor, werden hier aber vollständigkeitshalber
    /// mitgeführt; 0x30 ist der No-Op-Füllwert, alles andere (inkl. Terminator 0x31) ohne Wirkung.</summary>
    public static void ApplyScancode(byte scancode, InputState input, int caveWidth)
    {
        switch (scancode)
        {
            case 0x4D: input.PressRight(); break;
            case 0x4B: input.PressLeft(); break;
            case 0x50: input.PressDown(caveWidth); break;
            case 0x48: input.PressUp(caveWidth); break;
            case 0xCD: input.ReleaseRight(); break;
            case 0xCB: input.ReleaseLeft(); break;
            case 0xD0: input.ReleaseDown(); break;
            case 0xC8: input.ReleaseUp(); break;
            case 0x1D: input.PressGrab(); break;
            case 0x9D: input.ReleaseGrab(); break;
        }

        input.SettleIdleState();
    }
}
