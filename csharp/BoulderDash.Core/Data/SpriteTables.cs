using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Data;

/// <summary>
/// Statische Frame-Tabellen für die Sprite-Zuordnung, 1:1 aus Init_Pointer (src/INIT.CPP:138-208)
/// übernommen. z_zeiger (dort: 90 Einträge, hier nur die 77 tatsächlich belegten 0-76) ordnet
/// "geordnete" Animationsframes den 49 Rohsprites aus SPRITES.BIN zu; DefaultFrame liefert je
/// Element den z_zeiger-Index, den ein frisch geladenes, noch nicht angelaufenes Spiel zeigt
/// (buffer[MASK_*]-Initialwerte, INIT.CPP:187-202, inklusive der Sonderbehandlung des Ausgangs).
/// </summary>
public static class SpriteTables
{
    /// <summary>z_zeiger[i] -> Index in die 49 Rohsprites aus SPRITES.BIN.</summary>
    public static readonly int[] FrameToRawSprite =
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, // 0-10
        11, // 11 Mauer
        12, // 12 Stahl
        13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, // 13-23 Rockford (12 Frames)
        24, 25, 26, 27, // 24-27 Lava
        24, 25, 26, 27, // 28-31 Lava (Wiederholung)
        28, 28, 29, 29, 30, 30, 31, 31, // 32-39 Geist
        32, 32, 33, 34, 34, 33, 32, 32, // 40-47 Motyl
        12, 35, // 48-49 Haus ein
        12, 35, // 50-51 Haus aus
        36, 36, 37, 38, 38, 37, 36, 36, // 52-59 Explo1
        39, 40, 41, 42, 39, 40, 41, 42, // 60-67 Maueru
        43, 44, 44, 45, 46, 46, 47, 47, // 68-75 Explo2
        48, // 76 Stahl-lauf
    ];

    /// <summary>
    /// Frame-Index (in FrameToRawSprite) je Element, wie unmittelbar nach level_laden sichtbar:
    /// die statischen Init_Pointer-Zuweisungen (INIT.CPP:187-202), überlagert von der
    /// Ausgangs-Tarnung aus level_laden ("buffer[MASK_HAUSA]=buffer[MASK_STAHL]", BOULDER.CPP:1000) —
    /// der Ausgang sieht beim Laden identisch zur Stahlwand aus, bis ende() ihn freischaltet.
    /// </summary>
    public static int GetDefaultFrame(Element element) => element switch
    {
        Element.Empty => 0,
        Element.Earth => 1,
        Element.Boulder => 2,
        Element.Jewel => 3,
        Element.Wall => 11,
        Element.TitaniumWall => 12,
        Element.Rockford => 13,
        Element.Amoeba => 24,
        Element.Firefly => 32,
        Element.Butterfly => 40,
        Element.Entrance => 48,
        Element.EscapeDoor => 12, // getarnt als Stahlwand, siehe Klassenkommentar
        Element.Explosion => 52,
        Element.EnchantedWall => 11, // wie Mauer, bis mrun die Animation aktiviert
        Element.JewelExplosion => 68,
        Element.BorderFill => 76,
        _ => throw new ArgumentOutOfRangeException(nameof(element), element, null),
    };
}
