namespace BoulderDash.Core.Data;

/// <summary>Quelle fertig aufgebauter Cave-Kachelkarten, unabhängig vom Speicherformat.
/// Der Name ist der Bezeichner einer Cave in einem bestimmten Schwierigkeitsgrad, z.B. "cave-A-1"
/// (Groß-/Kleinschreibung egal).</summary>
public interface ICaveRepository
{
    CaveData Get(string name);
}
