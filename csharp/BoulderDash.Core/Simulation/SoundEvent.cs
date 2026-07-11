namespace BoulderDash.Core.Simulation;

/// <summary>
/// Sound-Ereignisse, die CavePhysics/GameTick auslösen können. Core bleibt audiofrei — die
/// Game-Schicht (AudioPlayer) konsumiert diese über GameState.SoundEvents und spielt passende
/// OGG-Dateien ab. Werte ohne zugehörige Datei bleiben bewusst stumm (siehe sound/-Ordner:
/// nur WalkEarth, WalkEmpty, CollectJewel, BoulderLand und Music sind aktuell vertont).
/// </summary>
public enum SoundEvent
{
    WalkEarth,
    WalkEmpty,
    CollectJewel,
    BoulderLand,
    JewelLand,
    Explosion,
    PushBoulder,
    Amoeba,
    EnchantedWall,
    EscapeDoorOpen,
    TimeWarning,
    EntranceExplosion,
    BonusCount,
    Death,
}
