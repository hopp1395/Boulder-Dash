using System.Globalization;

namespace BoulderDash.Core.Simulation;

/// <summary>Eine Anzeigefarbe mit 8 Bit pro Kanal.</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    /// <summary>Liest eine Farbe im Format <c>#RRGGBB</c> (Rautezeichen optional, Hex-Ziffern in beliebiger Schreibweise).</summary>
    public static bool TryParse(string text, out Rgb color)
    {
        color = default;
        var digits = text.AsSpan().Trim();
        if (digits.StartsWith("#"))
        {
            digits = digits[1..];
        }

        if (digits.Length != 6
            || !byte.TryParse(digits[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(digits[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(digits[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        color = new Rgb(r, g, b);
        return true;
    }

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}

/// <summary>
/// Farben, die nicht aus der Cave-Datei stammen.
///
/// Die vier Farben einer Cave stehen inzwischen als RGB-Werte in der Cave-Datei selbst
/// (<c>[Rules] Colors</c>, siehe CaveTextFile) — wie alle Spieldaten also WYSIWYG. Die frühere
/// 16-Farben-Tabelle mit 6-Bit-VGA-DAC-Werten (setnewpalette, src/BOULDER.CPP:496-512) und die
/// Umrechnung auf 8 Bit pro Kanal gibt es nicht mehr; ihre Ergebniswerte sind einmalig in die
/// Cave-Dateien eingeflossen.
/// </summary>
public static class Palette
{
    /// <summary>Blitz-Farben des Ausgangs (ende(), BOULDER.CPP:683-684): abwechselnd weiß und dunkel.
    /// Sie ersetzen die Farbe 0 der Cave-Palette, entsprechen den DAC-Tripeln 63,63,63 bzw. 8,8,8.</summary>
    public static readonly Rgb ExitFlashBright = new(0xFF, 0xFF, 0xFF);
    public static readonly Rgb ExitFlashDark = new(0x20, 0x20, 0x20);
}
