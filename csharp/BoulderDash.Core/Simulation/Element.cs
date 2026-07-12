namespace BoulderDash.Core.Simulation;

/// <summary>
/// Kachel-Grundelemente einer Cave, benannt nach dem Original-Handbuch (First Star Software).
/// Die Werte 0-15 entsprechen den MASK_*-Konstanten in src/BOULDER.CPP:41-56 (im Original die unteren
/// 4 Bits eines Kachelbytes — mehr passten dort nicht hinein).
///
/// <see cref="Void"/> hat keine Original-Entsprechung und sprengt als erstes Element diese 4 Bits;
/// Platz dafür ist, weil Bit 0x10 im Kachelbyte frei geblieben ist (siehe CaveObjects.ElementMask).
/// </summary>
public enum Element : byte
{
    Empty = 0,
    Earth = 1,
    Boulder = 2,
    Jewel = 3,
    Wall = 4,
    TitaniumWall = 5,
    Rockford = 6,
    Amoeba = 7,
    Firefly = 8,
    Butterfly = 9,
    Entrance = 10,
    EscapeDoor = 11,
    Explosion = 12,
    EnchantedWall = 13,
    JewelExplosion = 14,
    BorderFill = 15,

    /// <summary>Das Nichts außerhalb der Höhle — alles im Gitter, was nicht zur Cave gehört
    /// (siehe VoidObject). Keine Original-Entsprechung: Dort war jede Cave rechteckig.</summary>
    Void = 16,
}
