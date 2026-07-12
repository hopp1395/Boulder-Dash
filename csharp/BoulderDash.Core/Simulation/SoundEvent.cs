namespace BoulderDash.Core.Simulation;

/// <summary>
/// Sound-Ereignisse, die CavePhysics/GameTick auslösen können. Core bleibt audiofrei — die
/// Game-Schicht (AudioPlayer) konsumiert diese über GameState.SoundEvents und synthetisiert die
/// passenden Klänge (siehe Audio/SoundRecipes.cs, nach der Original-C64-Sound-Dokumentation).
/// PushBoulder hat keinen eigenen Klang, sondern den des aufschlagenden Steins ("A boulder sound
/// plays upon successful pushes", BDCFF 0006). Death bleibt bewusst stumm — der Tod klingt bereits
/// über den Explosion-Sound.
/// Amoeba/EnchantedWall sind Dauerklänge (Drones), keine einmaligen Trigger — AudioPlayer
/// leitet ihren Ein/Aus-Zustand direkt aus GameState ab, nicht aus dieser Queue.
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
    EscapeDoorOpen,
    TimeWarning,
    EntranceExplosion,
    BonusCount,
    Death,
    Uncover,
}
