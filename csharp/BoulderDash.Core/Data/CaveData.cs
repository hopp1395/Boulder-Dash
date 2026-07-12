using BoulderDash.Core.Objects;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Data;

/// <summary>
/// Eine geladene Cave: Kopfdaten plus Kachelkarte, gelesen aus einer Cave-Textdatei
/// (siehe CaveTextFile). Die Kacheln sind hier noch roh (reine Element-IDs ohne Verarbeitungs-/
/// Bewegungs-Flags), wie unmittelbar nach dem Laden.
/// </summary>
public sealed class CaveData
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required char Letter { get; init; }
    public required bool IsIntermission { get; init; }

    /// <summary>Breite und Höhe des Gitters — der umschließende Rahmen der Cave, nicht die Cave
    /// selbst: Sie darf darin beliebig geformt sein, alles um sie herum ist Element.Void. Beides ist
    /// bewusst kein byte mehr; die 40x22 des Originals sind nur noch die Größe der BD1-Caves, keine
    /// Grenze (siehe CaveTextFile).</summary>
    public required int Width { get; init; }
    public required int Height { get; init; }

    public required byte JewelQuota { get; init; }
    public required byte TimeSeconds { get; init; }
    /// <summary>Die 4 Farben der Cave-Palette (Palettenindex 0-3), als RGB-Werte aus der Cave-Datei.</summary>
    public required Rgb[] Colors { get; init; }
    public required byte EnchantedWallSeconds { get; init; }

    /// <summary>Spielsekunden, die die Amoeba langsam wächst (3 %), bevor sie auf 25 % umschaltet.
    /// In BD1 steht dieser Wert im selben Cave-Kopf-Byte $01 wie die Zaubermauer-Zeit — beide sollten
    /// laut Original-Datenformat nie in derselben Cave vorkommen. Siehe AmoebaObject.</summary>
    public required byte AmoebaSlowGrowthSeconds { get; init; }

    public required byte PointsPerJewelBeforeQuota { get; init; }
    public required byte PointsPerJewelAfterQuota { get; init; }

    /// <summary>Spieltempo: hängt in BD1 am Schwierigkeitsgrad und an der Cave-Art, nicht an der
    /// Cave selbst — da eine CaveData genau eine Cave auf genau einem Level ist (Level steht im
    /// Cave-Kopf), steht es trotzdem hier. Siehe CaveSpeed.</summary>
    public required CaveSpeed GameSpeed { get; init; }

    /// <summary>Kachelkarte, zeilenweise, Länge = Width*Height, ein Byte = eine Element-ID.</summary>
    public required byte[] Tiles { get; init; }

    public Element GetElement(int x, int y) => (Element)(Tiles[y * Width + x] & CaveObjects.ElementMask);

    /// <summary>
    /// Sucht die erste Kachel mit dem angegebenen Element (zeilenweise, wie die
    /// Original-Suchschleifen für Ein-/Ausgang in level_laden, BOULDER.CPP:1032-1035).
    /// </summary>
    public int FindFirstIndexOf(Element element)
    {
        var raw = (byte)element;
        for (var i = 0; i < Tiles.Length; i++)
        {
            if (Tiles[i] == raw)
            {
                return i;
            }
        }

        return -1;
    }
}
