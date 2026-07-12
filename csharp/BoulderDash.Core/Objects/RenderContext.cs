using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Was ein <see cref="CaveObject"/> zum Zeichnen braucht, aber nicht selbst besitzt: Zustände, die
/// der ganzen Cave gehören, nicht der einzelnen Kachel. Die Liste ist bewusst kurz und ausdrücklich
/// aufgezählt statt einfach GameState durchzureichen — sie ist damit die vollständige Aufstellung
/// dessen, was die Objekte noch von außen mitbekommen müssen.
/// </summary>
/// <param name="Clk4">Türblinken von Ein- und Ausgang (sprites_wechsel(), BOULDER.CPP:598-603).</param>
/// <param name="ExitFlashOn">Ob der Ausgang freigeschaltet ist und blinkt (ende(), :681-687).</param>
/// <param name="EnchantedWallRunning">Ob die Zaubermauer gerade läuft und mahlt.</param>
/// <param name="Input">Rockfords Bewegungseingabe — sie bestimmt Laufzyklus und Blickrichtung.</param>
public readonly record struct RenderContext(
    byte Clk4,
    bool ExitFlashOn,
    bool EnchantedWallRunning,
    InputState Input);
