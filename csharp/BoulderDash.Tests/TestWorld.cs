using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

/// <summary>
/// Baut Gitter UND Welt für einen Test.
///
/// Seit dem Objektmodell ist die Cave beides zugleich: Sie hält neben den Objekten auch
/// <see cref="GameState"/>, <see cref="InputState"/>, <see cref="Camera"/> und den Zufallsstrom —
/// sonst käme ein parameterloses <c>Interact()</c> nicht daran. Tests greifen über
/// <c>cave.State</c>, <c>cave.Input</c> und <c>cave.Camera</c> darauf zu, statt diese Dinge wie
/// früher einzeln an die Physik zu reichen.
/// </summary>
internal static class TestWorld
{
    /// <summary>Eine spielfertige Cave. <paramref name="random"/> setzt den Zufallsstrom, aus dem
    /// Amoeba-Wachstum und Steinschub würfeln (Seed 1 wie in GameSession); Tests, die einen Wurf
    /// erzwingen wollen, reichen hier ihren eigenen Würfel herein.</summary>
    public static Cave NewCave(CaveData data, Random? random = null)
    {
        var state = new GameState();
        state.ResetForCave(data);
        return new Cave(data, state, new InputState(), new Camera(), random ?? new Random(1));
    }

    /// <summary>Der Takt dazu. Ohne eigene <paramref name="cover"/> ist nichts verdeckt — die Physik
    /// läuft dann los, sobald der Eingang steht (siehe GameTick).</summary>
    public static GameTick NewTick(Random random, ScreenCover? cover = null) =>
        new(cover ?? new ScreenCover(random), new ExploreMap(), random);
}
