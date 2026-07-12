using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Was fallen kann: Stein und Diamant. Sie teilen sich das komplette Fallverhalten (regel(),
/// BOULDER.CPP:842-887) — sie fallen in Leerraum, rollen von abgerundeten Nachbarn ab, erschlagen
/// Rockford, wenn sie fallend auf ihm landen, und werden von der Zaubermauer in ihr Gegenstück
/// umgewandelt. Sie unterscheiden sich nur darin, WAS dabei herauskommt und wie es klingt.
/// </summary>
public abstract class FallingObject : CaveObject
{
    /// <summary>Bit 0x40: Das Objekt fällt gerade wirklich (statt nur zu liegen). Nur ein fallendes
    /// erschlägt Rockford, löst den Landeklang aus und wird von der Zaubermauer umgewandelt.</summary>
    public bool Falling { get; set; }

    /// <summary>Abgerundet ist nur ein RUHENDER Stein/Diamant (BDCFF 0000). Von einem, der selbst
    /// noch fällt, rollt nichts ab — darauf wird gelandet. Das DOS-Original prüfte hier nur die
    /// Element-ID und ließ deshalb auch von fallenden Objekten abrollen.</summary>
    public override bool IsRounded => !Falling;

    /// <summary>Der Klang, wenn dieses Objekt aufschlägt.</summary>
    public abstract SoundEvent LandingSound { get; }

    /// <summary>Was die Zaubermauer daraus macht: Stein wird Diamant, Diamant wird Stein. Das
    /// Ergebnis fällt zwei Zeilen unter der Mauer weiter, ist also schon in Bewegung.</summary>
    public abstract FallingObject EnchantedWallProduct();

    public override byte ToRaw() => (byte)(base.ToRaw() | (Falling ? 0x40 : 0));
}
