using BoulderDash.Core.Data;

namespace BoulderDash.Core.Simulation;

/// <summary>
/// Veränderlicher Fortschritt einer laufenden Cave: Analog zu den in src/BOULDER.CPP:74-141
/// deklarierten globalen Spielvariablen, die level_laden bei jedem Cave-Start zurücksetzt
/// (BOULDER.CPP:976-998). Feldnamen bleiben nah am Original, damit die Spielregeln in den Objekten
/// direkt nachvollziehbar bleiben.
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

    /// <summary>Restliche Spielsekunden, in denen die Amoeba noch langsam wächst; bei 0 schaltet sie
    /// auf schnelles Wachstum um (BD1 "amoeba slow growth time", siehe AmoebaObject).</summary>
    public byte AmoebaSlowGrowthRemaining { get; set; }

    /// <summary>Amoeba-Zellen, die der vorige Cave-Scan gezählt hat. Die Umwandlung greift laut Spec
    /// erst im Folge-Scan, entscheidet also immer auf Basis der Zahlen des vorigen.</summary>
    public int AmoebaCountLastScan { get; set; }

    /// <summary>Ob im vorigen Scan zwar Amoeba existierte, aber keine einzige Zelle einen freien
    /// Nachbarn hatte ("suffocated") — dann wird sie im nächsten Scan zu Jewels.</summary>
    public bool AmoebaSuffocatedLastScan { get; set; }

    public bool IsCaveEnded { get; set; }
    public bool AdvanceToNextCave { get; set; }

    /// <summary>anfang_var: Fortschritt des Eingangsaufbaus/Dissolve (0..~101+).</summary>
    public byte EntranceProgress { get; set; }

    /// <summary>stat: 0 solange Rockford im letzten Sweep gefunden wurde, sonst 1 (Todeserkennung).</summary>
    public byte Stat { get; set; }

    public bool ExitFlashOn { get; set; }

    /// <summary>Ob gerade auf- oder zugedeckt wird (ScreenCover) — der Uncover-Sound übertönt
    /// dann alle anderen Sounds, siehe AudioPlayer.</summary>
    public bool ScreenCoverActive { get; set; }

    /// <summary>Überschreibt Palettenfarbe 0 während des Ausgangs-Blitzes (ende(), BOULDER.CPP:683-684). Null = normale Cave-Farbe.</summary>
    public Rgb? PaletteColor0Override { get; set; }

    // Die Animationszähler des Originals (wechsel_vier, wechsel_explo, wechsel_boulder) standen
    // früher hier: EIN Zähler für alle Objekte. Sie sind in die Objekte selbst gewandert und laufen
    // dort weiter (CaveObject.AnimationPhase, ExplosionObject.ExplosionPhase, RockfordObject) — den
    // gemeinsamen Takt führt die Cave (Cave.AnimationPhase).

    /// <summary>Sound-Ereignisse dieses Ticks, von den Objekten befüllt und von der
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
        AmoebaPresent = false;
        AmoebaSlowGrowthRemaining = cave.AmoebaSlowGrowthSeconds;
        AmoebaCountLastScan = 0;
        AmoebaSuffocatedLastScan = false;
        IsCaveEnded = false;
        AdvanceToNextCave = false;
        EntranceProgress = 0;
        Stat = 0;
        ExitFlashOn = false;
        ScreenCoverActive = false;
        PaletteColor0Override = null;
    }
}
