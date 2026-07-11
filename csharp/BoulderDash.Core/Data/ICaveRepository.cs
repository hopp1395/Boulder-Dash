namespace BoulderDash.Core.Data;

/// <summary>Quelle fertig aufgebauter Cave-Kachelkarten, unabhängig vom Speicherformat.</summary>
public interface ICaveRepository
{
    CaveData Get(Cave cave, CaveLevel level);
}
