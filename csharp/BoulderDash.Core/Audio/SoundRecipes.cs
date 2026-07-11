namespace BoulderDash.Core.Audio;

/// <summary>
/// Klangparameter der Original-Boulder-Dash-Soundeffekte, nach Peter Broadribbs Analyse der
/// SID-Registerwerte (https://www.elmerproductions.com/sp/peterb/sounds.html). Kurze,
/// funktionale Effekte (Frequenz + Hüllkurve); die dort ebenfalls dokumentierte 128-Noten-
/// Titelmelodie liegt separat in <see cref="ThemeTune"/> (eigene Notendaten, nutzt aber
/// <see cref="RegisterToHz"/> von hier).
///
/// HoldSeconds/ReleaseSeconds sind bei "sustain at volume X" ohne dokumentierte Gate-Länge
/// kalibriert (siehe Envelope-Klassenkommentar), ebenso SustainLevel wo die Doku keinen Wert
/// nennt (Amoeba-Drone) — jeweils einzeln kommentiert.
/// </summary>
public static class SoundRecipes
{
    /// <summary>Register→Hz, aus den Beispiel-Umrechnungen der Doku abgeleitet (z.B. $1432=5170→315,2 Hz: 5170*f=315,2).</summary>
    public const double RegisterToHz = 0.060968;

    private const double CalibratedHold = 0.03;
    private const double CalibratedRelease = 0.05;

    public static readonly Envelope Explosion = new()
    {
        AttackSeconds = 0.008, DecaySeconds = 2.4, SustainLevel = 0.0, ReleaseSeconds = 0.006,
    };
    public const double ExplosionHz = 315.2;

    public static readonly Envelope JewelLand = new()
    {
        AttackSeconds = 0.002, DecaySeconds = 0.006, SustainLevel = 10.0 / 15,
        HoldSeconds = CalibratedHold, ReleaseSeconds = CalibratedRelease,
    };
    public const double JewelLandHzMin = 2091.5;
    public const double JewelLandHzMax = 3980.0;

    public static readonly Envelope CollectJewel = new()
    {
        AttackSeconds = 0.002, DecaySeconds = 0.006, SustainLevel = 15.0 / 15,
        HoldSeconds = CalibratedHold, ReleaseSeconds = CalibratedRelease,
    };
    public const double CollectJewelHz = 319.5;

    public static readonly Envelope BoulderLand = new()
    {
        AttackSeconds = 0.002, DecaySeconds = 0.006, SustainLevel = 15.0 / 15,
        HoldSeconds = CalibratedHold, ReleaseSeconds = CalibratedRelease,
    };
    public const double BoulderLandHz = 143.5;

    /// <summary>Crack: Eingangs-"Geburt" (Rockford erscheint) und Ausgangs-Aktivierung.</summary>
    public static readonly Envelope Crack = new()
    {
        AttackSeconds = 0.008, DecaySeconds = 0.75, SustainLevel = 0.0, ReleaseSeconds = 0.024,
    };
    public const double CrackHz = 736.6;

    public static readonly Envelope TimeWarning = new()
    {
        AttackSeconds = 0.002, DecaySeconds = 1.5, SustainLevel = 0.0, ReleaseSeconds = 0.01,
    };
    public const double TimeWarningHzAt9Seconds = 468.2;

    /// <summary>+$0100 Registerschritt je Sekunde ≈ 15,6 Hz (aus 468,2→608,7 Hz über 9 Stufen abgeleitet).</summary>
    public const double TimeWarningHzStep = (608.7 - 468.2) / 9.0;

    public static readonly Envelope Uncover = new()
    {
        AttackSeconds = 0.002, DecaySeconds = 0.168, SustainLevel = 0.0, ReleaseSeconds = 0.01,
    };
    public const double UncoverHzMin = 1560.8;
    public const double UncoverHzMax = 3543.1;

    /// <summary>SustainLevel nicht dokumentiert (nur Frequenz/Attack/Decay angegeben) — moderat kalibriert, da Hintergrund-Drone.</summary>
    public static readonly Envelope AmoebaDrone = new()
    {
        AttackSeconds = 0.024, DecaySeconds = 0.006, SustainLevel = 8.0 / 15,
        HoldSeconds = CalibratedHold, ReleaseSeconds = CalibratedRelease,
    };
    public const double AmoebaDroneHzMin = 124.9;
    public const double AmoebaDroneHzMax = 234.1;

    public static readonly Envelope EnchantedWallDrone = new()
    {
        AttackSeconds = 0.002, DecaySeconds = 0.006, SustainLevel = 10.0 / 15,
        HoldSeconds = CalibratedHold, ReleaseSeconds = CalibratedRelease,
    };
    public const double EnchantedWallDroneHzMin = 2091.5;
    public const double EnchantedWallDroneHzMax = 2481.7;

    public static readonly Envelope BonusSweepNote = new()
    {
        AttackSeconds = 0.0, DecaySeconds = 0.0, SustainLevel = 1.0, HoldSeconds = 0.001, ReleaseSeconds = 0.0,
    };

    public static readonly Envelope Movement = new()
    {
        AttackSeconds = 0.024, DecaySeconds = 0.006, SustainLevel = 12.0 / 15,
        HoldSeconds = CalibratedHold, ReleaseSeconds = CalibratedRelease,
    };
    public const double WalkEmptyHz = 827.2;
    public const double WalkEarthHz = 2575.6;

    /// <summary>Bonus-Sweep-Algorithmus aus der Doku: z startet bei $D0=208 und zählt je Sekunde
    /// (je BonusCount-Ereignis) um 1 herunter; pro Ereignis 15 Noten x=15..1 absteigend.</summary>
    public const int BonusSweepInitialZ = 0xD0;

    public static double BonusSweepNoteHz(int z, int x) => ((z - (x * 2)) * 256) * RegisterToHz;

    /// <summary>Zeitwarn-Tonhöhe für die angegebene verbleibende Sekundenzahl (0-9, wie in der Doku).</summary>
    public static double TimeWarningHz(int secondsRemaining)
    {
        var clamped = Math.Clamp(secondsRemaining, 0, 9);
        return TimeWarningHzAt9Seconds + (TimeWarningHzStep * (9 - clamped));
    }
}
