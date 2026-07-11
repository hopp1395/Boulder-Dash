namespace BoulderDash.Core.Simulation;

/// <summary>
/// Kachel-Grundelemente einer Cave, benannt nach dem Original-Handbuch (First Star Software).
/// Werte entsprechen den MASK_*-Konstanten in src/BOULDER.CPP:41-56 (untere 4 Bits eines Kachelbytes).
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
}
