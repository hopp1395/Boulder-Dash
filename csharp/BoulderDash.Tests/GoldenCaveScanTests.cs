using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

/// <summary>
/// Zweites Sicherheitsnetz neben <see cref="GoldenStateTests"/>: Die Demo läuft nur auf Cave A, und
/// die enthält weder Firefly noch Butterfly noch Amoeba noch Zaubermauer — genau die Objekte mit dem
/// kniffligsten Verhalten blieben also ungesichert. Dieser Test taktet stattdessen Caves headless
/// durch, in denen sie vorkommen, und friert einen Hash über ALLE durchlaufenen Gitterzustände ein
/// (nicht nur den Endzustand — auch die Explosionen, die zwischendurch aufblitzen und wieder
/// verschwinden, gehen so ein).
///
/// Rockford bekommt bewusst keine Eingabe: er bleibt im Eingang stehen, während die Kreaturen
/// patrouillieren, Steine fallen und die Amoeba wächst. Das deckt Kreaturen-Drehlogik,
/// Amoeba-Wachstum samt RNG-Ziehungen, Explosionsausbreitung und -auflösung sowie die Zaubermauer
/// ab, ohne dass ein Zugskript gepflegt werden müsste.
///
/// Schlägt der Test nach einer ABSICHTLICHEN Verhaltensänderung fehl, die Werte neu einfrieren (die
/// Testausgabe nennt den tatsächlich berechneten Hash). Schlägt er nach einem reinen Refactoring
/// fehl, ist das Refactoring nicht verhaltensgleich — dann den Fehler suchen, nicht den Hash
/// anpassen.
///
/// Herkunft der Werte: Sie sind gegen die Implementierung VOR dem Architektur-Refactoring
/// nachgerechnet (Stand b78dd67, noch mit CavePhysics) und stimmen mit dem heutigen Objektmodell
/// Bit für Bit überein — sie belegen also genau das, wozu dieser Test da ist. Die ursprünglich hier
/// eingetragenen Werte für die Caves G, H und N taten das nicht: Sie entstanden im selben Commit,
/// der das Testprojekt unkompilierbar zurückließ, und konnten deshalb nie geprüft werden.
/// </summary>
public class GoldenCaveScanTests
{
    /// <summary>Reicht für rund 640 Cave-Scans (die Physik läuft jeden 3. Tick) — genug, dass die
    /// Kreaturen ihre Runden drehen und die Amoeba sichtbar wächst.</summary>
    private const int Ticks = 2000;

    [Theory]
    // Die Kommentare nennen, was im eingefrorenen Lauf tatsächlich passiert — daran hängt der Wert
    // des jeweiligen Hashes. Fällt eine dieser Wirkungen künftig weg, sichert der Hash sie nicht mehr.
    [InlineData("cave-G-1", 534957559u)]  // 5 Fireflies: zwei explodieren; Amoeba wächst auf 204 Zellen und wird zu Boulders
    [InlineData("cave-H-1", 4180988487u)] // 4 Fireflies: einer explodiert; viele fallende Steine
    [InlineData("cave-M-1", 970702359u)]  // 30 Butterflies; Amoeba wächst von 1 auf 55 Zellen
    [InlineData("cave-N-1", 2480803819u)] // 6 Fireflies + 6 Butterflies, keiner explodiert
    [InlineData("cave-test-5", 2040212403u)] // Zaubermauer: wandelt um, verschluckt, klingt
    [InlineData("cave-test-8", 2037096967u)] // Explosion neben dem Ausgang (der verschont bleibt)
    public void Cave_laeuft_ohne_Eingabe_deterministisch_durch(string caveName, uint expectedHash)
    {
        var caves = new CaveTextRepository(Path.Combine(TestPaths.GameAssets, "Caves"));
        var data = caves.Get(caveName);

        // Derselbe feste Seed wie in GameSession — Amoeba-Wachstum und Steinschub würfeln daraus.
        var random = new Random(1);
        var cave = TestWorld.NewCave(data, random);
        var entranceIndex = cave.FindFirstIndexOf(Element.Entrance);
        var tick = TestWorld.NewTick(random);
        var clocks = new Clocks();

        var hash = 2166136261u; // FNV-1a Offset-Basis
        for (var i = 0; i < Ticks; i++)
        {
            tick.Tick(cave, clocks, entranceIndex);

            for (var t = 0; t < cave.Width * cave.Height; t++)
            {
                hash ^= cave.GetRaw(t);
                hash *= 16777619u;
            }
        }

        Assert.Equal(expectedHash, hash);
    }
}
