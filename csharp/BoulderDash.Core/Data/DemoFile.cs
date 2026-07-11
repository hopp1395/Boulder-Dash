namespace BoulderDash.Core.Data;

/// <summary>
/// Parser für DEMO.BIN — die Aufzeichnung, die im Original per F2 im Menü abgespielt wird
/// (demo(), src/BOULDER.CPP:337-378). Die Datei enthält mehrere CRLF-getrennte Aufzeichnungen,
/// aber `fread(f_zeiger,sizeof(BYTE),256,dz)` liest immer nur die ersten 256 Bytes — der
/// `nr`-Parameter von demo() ist toter Code, es gibt effektiv nur EINE abspielbare Demo
/// (Cave A). Byte-Inhalt: Tastatur-Scancodes wie in Mov_Rockford (0x48/0x4B/0x4D/0x50 Make,
/// 0xC8/0xCB/0xCD/0xD0 Break, 0x30 No-Op-Füllwert), terminiert durch 0x31.
/// </summary>
public static class DemoFile
{
    public const int Length = 256;

    public static byte[] Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var result = new byte[Length];
        Array.Copy(bytes, result, Math.Min(Length, bytes.Length));
        return result;
    }
}
