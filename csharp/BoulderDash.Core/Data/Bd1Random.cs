namespace BoulderDash.Core.Data;

/// <summary>
/// Der originale 8-Bit-Zufallsgenerator von Boulder Dash I (C64), 2x8-Bit-Zustand. Treibt die
/// Zufallsfüllung einer Cave (siehe CaveMapBuilder). Nachgebaut nach der dokumentierten Routine
/// bd_random in Boulder-Dash-C64/disassembly/tools/extract_data.py, die wiederum die Original-
/// Zufallsroutine des C64-Spiels reproduziert.
/// </summary>
public sealed class Bd1Random
{
    private int _s1;
    private int _s2;

    /// <summary>Startzustand wie im Original: s1=0, s2=der Level-Seed aus dem Cave-Header.</summary>
    public Bd1Random(byte seed)
    {
        _s1 = 0;
        _s2 = seed;
    }

    public byte Next()
    {
        var tmp1 = (_s1 & 1) * 0x80;
        var tmp2 = (_s2 >> 1) & 0x7F;

        var result = _s2 + (_s2 & 1) * 0x80;
        var carry = result > 0xFF ? 1 : 0;
        result = (result & 0xFF) + carry + 0x13;
        carry = result > 0xFF ? 1 : 0;
        _s2 = result & 0xFF;

        result = _s1 + carry + tmp1;
        carry = result > 0xFF ? 1 : 0;
        result = (result & 0xFF) + carry + tmp2;
        _s1 = result & 0xFF;

        return (byte)_s1;
    }
}
