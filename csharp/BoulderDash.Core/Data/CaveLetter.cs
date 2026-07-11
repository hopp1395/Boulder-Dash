namespace BoulderDash.Core.Data;

/// <summary>Umrechnung zwischen dem Cave-Enum und seinem Buchstaben (A-T); die Enum-Reihenfolge
/// entspricht dem Alphabet, siehe Cave.</summary>
public static class CaveLetter
{
    public static char ToChar(Cave cave) => (char)('A' + (int)cave);

    public static Cave FromChar(char letter) => (Cave)(char.ToUpperInvariant(letter) - 'A');

    /// <summary>Q-T sind die 4 Intermissions.</summary>
    public static bool IsIntermission(Cave cave) => cave >= Cave.CaveQ;
}
