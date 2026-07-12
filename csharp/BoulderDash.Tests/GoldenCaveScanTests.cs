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
/// </summary>
public class GoldenCaveScanTests
{
    /// <summary>Reicht für rund 640 Cave-Scans (die Physik läuft jeden 3. Tick) — genug, dass die
    /// Kreaturen ihre Runden drehen und die Amoeba sichtbar wächst.</summary>
    private const int Ticks = 2000;

    [Theory]
    // Die Kommentare nennen, was im eingefrorenen Lauf tatsächlich passiert — daran hängt der Wert
    // des jeweiligen Hashes. Fällt eine dieser Wirkungen künftig weg, sichert der Hash sie nicht mehr.
    [InlineData("cave-G-1", 1704599531u)] // 5 Fireflies: zwei explodieren; Amoeba erstickt und wandelt um
    [InlineData("cave-H-1", 2749169391u)] // 4 Fireflies: einer explodiert; viele fallende Steine
    [InlineData("cave-M-1", 970702359u)]  // 30 Butterflies; Amoeba wächst von 1 auf 55 Zellen
    [InlineData("cave-N-1", 3841041147u)] // 6 Fireflies + 6 Butterflies nebeneinander
    [InlineData("cave-test-5", 2040212403u)] // Zaubermauer: wandelt um, verschluckt, klingt
    [InlineData("cave-test-8", 2037096967u)] // Explosion neben dem Ausgang (der verschont bleibt)
    public void Cave_laeuft_ohne_Eingabe_deterministisch_durch(string caveName, uint expectedHash)
    {
        var caves = new CaveTextRepository(Path.Combine(TestPaths.GameAssets, "Caves"));
        var data = caves.Get(caveName);

        var cave = new Cave(data);
        var state = new GameState();
        state.ResetForCave(data);
        var entranceIndex = cave.FindFirstIndexOf(Element.Entrance);

        // Derselbe feste Seed wie in GameSession — Amoeba-Wachstum und Steinschub würfeln daraus.
        var random = new Random(1);
        var tick = new GameTick(new CavePhysics(random), new ScreenCover(random), random);
        var input = new InputState();
        var camera = new Camera();
        var clocks = new Clocks();

        var hash = 2166136261u; // FNV-1a Offset-Basis
        for (var i = 0; i < Ticks; i++)
        {
            tick.Tick(cave, state, input, camera, clocks, entranceIndex);

            for (var t = 0; t < cave.Width * cave.Height; t++)
            {
                hash ^= cave.GetRaw(t);
                hash *= 16777619u;
            }
        }

        Assert.Equal(expectedHash, hash);
    }
}
