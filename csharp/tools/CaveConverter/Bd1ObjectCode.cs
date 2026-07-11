using BoulderDash.Core.Simulation;

namespace CaveConverter;

/// <summary>
/// Ordnet einen rohen BD1-Objekt-Code (0-255, bei Zeichenbefehlen bereits per "&amp; 0x3F" auf
/// 0-63 reduziert) einem Element zu. BD1 reserviert je Objekttyp einen Codebereich für
/// Richtungs-/Animationsvarianten (z.B. 0x30-0x37 für Butterfly in verschiedenen Anfangsrichtungen);
/// da dieser Port keine Anfangsrichtung pro Kachel speichert (CavePhysics ermittelt die
/// Bewegungsrichtung zur Laufzeit selbst), werden alle Varianten auf dasselbe Element abgebildet:
/// "nächstliegende bekannte Basis kleiner-gleich Code".
/// </summary>
public static class Bd1ObjectCode
{
    private static readonly (byte Code, Element Element)[] Bases =
    [
        (0x00, Element.Empty),
        (0x01, Element.Earth),
        (0x02, Element.Wall),
        (0x03, Element.EnchantedWall),
        (0x04, Element.EscapeDoor),
        (0x07, Element.TitaniumWall),
        (0x08, Element.Firefly),
        (0x10, Element.Boulder),
        (0x14, Element.Jewel),
        (0x25, Element.Entrance),
        (0x30, Element.Butterfly),
        (0x3A, Element.Amoeba),
    ];

    public static Element ToElement(byte code)
    {
        var result = Bases[0].Element;
        foreach (var (baseCode, element) in Bases)
        {
            if (baseCode > code)
            {
                break;
            }

            result = element;
        }

        return result;
    }
}
