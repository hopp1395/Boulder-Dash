namespace BoulderDash.Core.Data;

/// <summary>ICaveRepository-Implementierung für das Cave-Textformat: liest beim Erzeugen alle
/// cave-*.txt-Dateien eines Verzeichnisses ein; der Dateiname ohne Endung (z.B. "cave-A-1") ist
/// der Name, unter dem die Cave abrufbar ist. Neue Caves erfordern daher nur eine neue Datei.</summary>
public sealed class CaveTextRepository : ICaveRepository
{
    private readonly Dictionary<string, CaveData> _caves = new(StringComparer.OrdinalIgnoreCase);

    public CaveTextRepository(string cavesDirectory)
    {
        foreach (var path in Directory.EnumerateFiles(cavesDirectory, "cave-*.txt"))
        {
            _caves[Path.GetFileNameWithoutExtension(path)] = CaveTextFile.Parse(File.ReadAllText(path), path);
        }
    }

    /// <summary>Namen aller geladenen Caves (Dateinamen ohne Endung).</summary>
    public IReadOnlyCollection<string> Names => _caves.Keys;

    public CaveData Get(string name) => _caves.TryGetValue(name, out var cave)
        ? cave
        : throw new KeyNotFoundException($"Cave '{name}' nicht gefunden (erwartet wird eine Datei {name}.txt im Cave-Verzeichnis).");
}
