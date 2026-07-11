namespace BoulderDash.Core.Data;

/// <summary>ICaveRepository-Implementierung für das Cave-Textformat: liest beim Erzeugen alle
/// cave-{Buchstabe}-{Level}.txt-Dateien aus einem Verzeichnis und baut daraus die Kachelkarten.</summary>
public sealed class CaveTextRepository : ICaveRepository
{
    private readonly Dictionary<(Cave Cave, CaveLevel Level), CaveData> _caves = [];

    public CaveTextRepository(string cavesDirectory)
    {
        foreach (Cave cave in Enum.GetValues<Cave>())
        {
            foreach (CaveLevel level in Enum.GetValues<CaveLevel>())
            {
                var path = Path.Combine(cavesDirectory, FileName(cave, level));
                _caves[(cave, level)] = CaveTextFile.Parse(File.ReadAllText(path), path);
            }
        }
    }

    public CaveData Get(Cave cave, CaveLevel level) => _caves[(cave, level)];

    public static string FileName(Cave cave, CaveLevel level) => $"cave-{CaveLetter.ToChar(cave)}-{(int)level}.txt";
}
