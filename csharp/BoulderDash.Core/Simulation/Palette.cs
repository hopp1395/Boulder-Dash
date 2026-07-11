namespace BoulderDash.Core.Simulation;

/// <summary>Eine VGA-DAC-Farbe nach Umrechnung auf 8-Bit-pro-Kanal für die Anzeige.</summary>
public readonly record struct Rgb(byte R, byte G, byte B);

/// <summary>
/// Feste 16-Farben-Tabelle und Umrechnung der 6-Bit-VGA-DAC-Werte, wie in setnewpalette
/// (src/BOULDER.CPP:496-512) verwendet. Jede Cave wählt 4 dieser 16 Farben über die
/// Header-Bytes 4-7 (BaseColors) aus.
///
/// Kanalreihenfolge verifiziert: setnewpalette ruft setvgapalette(fn, f1, f2, f3) mit
/// f1=Tabellenwert[1], f2=Tabellenwert[2], f3=Tabellenwert[0]; setvgapalette selbst lädt
/// diese in CH=g, CL=r, DH=b und ruft BIOS AH=10h/AL=10h auf, wo DH=Rot, CH=Grün, CL=Blau
/// gilt. Die beiden Vertauschungen heben sich exakt auf: Netto ist Tabellenwert[0]=Rot,
/// [1]=Grün, [2]=Blau — trotz irreführender Parameternamen keine tatsächliche Vertauschung.
/// </summary>
public static class Palette
{
    /// <summary>16 Grundfarben als rohe (R,G,B)-Tripel, teils außerhalb des gültigen 6-Bit-Bereichs
    /// (Farbe 9: Grün=68 statt max. 63) — ein Original-Datenfehler, der durch die 6-Bit-Breite
    /// des echten VGA-DACs stillschweigend auf 4 abgeschnitten wird (68 &amp; 0x3F = 4) und hier
    /// bewusst repliziert wird.</summary>
    private static readonly (int R, int G, int B)[] Colors16 =
    [
        (8, 8, 8), (63, 63, 63), (46, 8, 8), (28, 63, 63),
        (46, 8, 46), (8, 46, 8), (8, 8, 46), (63, 63, 8),
        (46, 28, 8), (36, 68, 8), (63, 28, 28), (28, 28, 28),
        (36, 36, 36), (28, 63, 28), (28, 28, 63), (46, 46, 46),
    ];

    /// <summary>Rechnet einen rohen 6-Bit-DAC-Kanalwert (mit Original-Überlaufmaskierung) auf 0-255 um.</summary>
    private static byte ToDisplayChannel(int raw)
    {
        var sixBit = raw & 0x3F;
        return (byte)(sixBit * 255 / 63);
    }

    public static Rgb FromRaw(int r, int g, int b) =>
        new(ToDisplayChannel(r), ToDisplayChannel(g), ToDisplayChannel(b));

    public static Rgb ForColorIndex(byte colorIndex)
    {
        var (r, g, b) = Colors16[colorIndex];
        return FromRaw(r, g, b);
    }

    /// <summary>Baut die 4 aktiven Farben einer Cave aus deren BaseColors-Header-Feld.</summary>
    public static Rgb[] BuildCavePalette(byte[] baseColors)
    {
        var result = new Rgb[baseColors.Length];
        for (var i = 0; i < baseColors.Length; i++)
        {
            result[i] = ForColorIndex(baseColors[i]);
        }

        return result;
    }

    /// <summary>Blitz-Farben des Ausgangs (ende(), BOULDER.CPP:683-684): abwechselnd weiß und dunkel.</summary>
    public static readonly Rgb ExitFlashBright = FromRaw(63, 63, 63);
    public static readonly Rgb ExitFlashDark = FromRaw(8, 8, 8);
}
