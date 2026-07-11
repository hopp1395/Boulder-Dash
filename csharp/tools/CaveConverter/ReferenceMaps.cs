namespace CaveConverter;

/// <summary>Liest die vorab dekodierten Referenzkarten aus Boulder-Dash-C64/extracted/caves/cave_*.txt
/// (je 5 Level à 22 Kartenzeilen, getrennt durch ";"-Kommentarzeilen und Leerzeilen) - dient nur der
/// Validierung des eigenen Decoders gegen eine unabhängig erzeugte Referenz.</summary>
public static class ReferenceMaps
{
    public static string[][] Load(string path)
    {
        var mapLines = File.ReadAllLines(path)
            .Where(line => line.Length > 0 && !line.StartsWith(';'))
            .ToArray();

        const int rows = 22;
        var levels = new string[mapLines.Length / rows][];
        for (var level = 0; level < levels.Length; level++)
        {
            levels[level] = mapLines[(level * rows)..((level + 1) * rows)];
        }

        return levels;
    }
}
