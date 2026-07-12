using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Data;

/// <summary>
/// Die z_zeiger-Tabelle der Sprite-Schicht, 1:1 aus Init_Pointer (src/INIT.CPP:138-208) übernommen
/// (dort 90 Einträge, hier nur die 77 tatsächlich belegten 0-76): Sie ordnet die "geordneten"
/// Animationsframes den 49 Rohsprites aus den Sprite-Textdateien zu (Reihenfolge siehe
/// SpriteTextRepository.Manifest).
///
/// WELCHEN Frame ein Objekt zeigt, steht nicht mehr hier, sondern beim Objekt selbst
/// (CaveObject.DefaultFrame und CaveObject.Appearance).
/// </summary>
public static class SpriteTables
{
    /// <summary>z_zeiger[i] -> Index in die 49 Rohsprites (ISpriteRepository.RawSprites).</summary>
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
}
