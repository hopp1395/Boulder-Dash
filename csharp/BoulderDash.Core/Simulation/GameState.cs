using BoulderDash.Core.Data;

namespace BoulderDash.Core.Simulation;

/// <summary>
/// Veränderlicher Fortschritt einer laufenden Cave: Analog zu den in src/BOULDER.CPP:74-141
/// deklarierten globalen Spielvariablen, die level_laden bei jedem Cave-Start zurücksetzt
/// (BOULDER.CPP:976-998). Feldnamen bleiben nah am Original, damit CavePhysics direkt
/// nachvollziehbar bleibt.
/// </summary>
public sealed class GameState
{
    public int Score { get; set; }
    public byte Chances { get; set; } = 3;

    public byte JewelsCollected { get; set; }
    public byte JewelQuota { get; private set; }
    public byte CaveTimeRemaining { get; set; }

    /// <summary>Aktueller Punktwert pro Diamant (pkt_d1) — wird nach Quotenerfüllung dauerhaft auf
    /// PointsPerJewelAfterQuota umgestellt (regel(), BOULDER.CPP:906).</summary>
    public byte CurrentJewelPoints { get; set; }
    public byte PointsPerJewelAfterQuota { get; private set; }

    public byte EnchantedWallTimeRemaining { get; set; }
    public bool EnchantedWallRunning { get; set; }

    /// <summary>Ob die Cave noch mindestens eine Amoeba-Zelle enthält — steuert die
    /// Amoeba-Dauerklang-Drone (nur zusammen mit EntranceProgress&gt;99, siehe AudioPlayer).</summary>
    public bool AmoebaPresent { get; set; }

    public bool IsCaveEnded { get; set; }
    public bool AdvanceToNextCave { get; set; }

    /// <summary>anfang_var: Fortschritt des Eingangsaufbaus/Dissolve (0..~101+).</summary>
    public byte EntranceProgress { get; set; }

    /// <summary>stat: 0 solange Rockford im letzten Sweep gefunden wurde, sonst 1 (Todeserkennung).</summary>
    public byte Stat { get; set; }

    public bool ExitFlashOn { get; set; }

    /// <summary>Überschreibt Palettenfarbe 0 während des Ausgangs-Blitzes (ende(), BOULDER.CPP:683-684). Null = normale Cave-Farbe.</summary>
    public Rgb? PaletteColor0Override { get; set; }

    /// <summary>wechsel_explo: Explosionsanimations-/Auflösungszähler, gemeinsam für alle Explosionen.</summary>
    public byte WechselExplo { get; set; }

    /// <summary>wechsel_vier: gemeinsamer Animationstakt für Diamant/Amoeba/Geist/Schmetterling/
    /// Zaubermauer/Rand-Gleitfenster, Periode 8 (sprites_wechsel(), BOULDER.CPP:593-607).</summary>
    public byte WechselVier { get; set; }

    /// <summary>wechsel_boulder: Rockford-Laufzyklus, Periode 6, läuft nur während einer aktiven
    /// Bewegungsrichtung (boulder_lauf(), BOULDER.CPP:611-646).</summary>
    public byte WechselBoulder { get; set; }

    /// <summary>Sound-Ereignisse dieses Ticks, von CavePhysics befüllt und von der
    /// Game-Schicht (AudioPlayer) pro Frame geleert. Core selbst bleibt audiofrei.</summary>
    public Queue<SoundEvent> SoundEvents { get; } = new();

    public void ResetForCave(CaveData cave)
    {
        JewelsCollected = 0;
        JewelQuota = cave.JewelQuota;
        CaveTimeRemaining = cave.TimeSeconds;
        CurrentJewelPoints = cave.PointsPerJewelBeforeQuota;
        PointsPerJewelAfterQuota = cave.PointsPerJewelAfterQuota;
        EnchantedWallTimeRemaining = cave.EnchantedWallSeconds;
        EnchantedWallRunning = false;
        IsCaveEnded = false;
        AdvanceToNextCave = false;
        EntranceProgress = 0;
        Stat = 0;
        ExitFlashOn = false;
        PaletteColor0Override = null;
        WechselExplo = 0;
        WechselVier = 0;
        WechselBoulder = 0;
    }
}
