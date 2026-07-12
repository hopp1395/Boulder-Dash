using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

/// <summary>
/// Prüft die 100 ausgelieferten BD1-Caves gegen die Rohdaten-Spezifikation
/// (elmerproductions.com/sp/peterb/rawCaveData.html).
///
/// Hintergrund: Der dort veröffentlichte Referenz-Decoder (decodecaves.c) zeichnet die Randmauer
/// ERST NACH den Cave-Objekten — laut Formatbeschreibung derselben Seite (und laut C64-Original)
/// gehört sie aber davor. Dadurch radiert er jeden Ausgang weg, der in der Randmauer liegt, und
/// das ist bei der Hälfte der Caves der Fall. Die Caves C, E, G, H, J, L, N und P waren so lange
/// unlösbar, bis die Karten hier neu aus den Rohdaten erzeugt wurden.
/// </summary>
public class CaveAssetTests
{
    private static readonly CaveTextRepository Caves = new(Path.Combine(TestPaths.GameAssets, "Caves"));

    public static TheoryData<string> AlleCaves()
    {
        var data = new TheoryData<string>();
        for (var letter = 'A'; letter <= 'T'; letter++)
        {
            for (var level = 1; level <= 5; level++)
            {
                data.Add($"cave-{letter}-{level}");
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AlleCaves))]
    public void Jede_Cave_hat_genau_einen_Eingang_und_genau_einen_Ausgang(string name)
    {
        var cave = Caves.Get(name);

        Assert.Equal(1, Count(cave, Element.Entrance));
        Assert.Equal(1, Count(cave, Element.EscapeDoor));
    }

    /// <summary>Die Randmauer ist geschlossen: jede Randkachel ist Stahl — einzige Ausnahme ist der
    /// Ausgang, der dort als Stahlwand getarnt sitzt (SpriteTables) und erst beim Blinken auffällt.</summary>
    [Theory]
    [MemberData(nameof(AlleCaves))]
    public void Die_Randmauer_ist_bis_auf_den_Ausgang_geschlossen(string name)
    {
        var cave = Caves.Get(name);

        for (var y = 0; y < cave.Height; y++)
        {
            for (var x = 0; x < cave.Width; x++)
            {
                if (x != 0 && x != cave.Width - 1 && y != 0 && y != cave.Height - 1)
                {
                    continue;
                }

                var element = cave.GetElement(x, y);
                Assert.True(
                    element is Element.TitaniumWall or Element.EscapeDoor,
                    $"{name}: Randkachel ({x},{y}) ist {element}.");
            }
        }
    }

    /// <summary>Die acht Caves, deren Ausgang laut Rohdaten in der Randmauer liegt (Spalte 0 bzw. 39) —
    /// genau die, die der Referenz-Decoder verschluckt. Die Position hängt nicht vom Grad ab, weil sie
    /// aus einem expliziten Objekt-Datensatz stammt und nicht aus der Zufallsfüllung.</summary>
    [Theory]
    [InlineData('C', 39, 18)]
    [InlineData('E', 39, 20)]
    [InlineData('G', 39, 5)]
    [InlineData('H', 0, 3)]
    [InlineData('J', 39, 20)]
    [InlineData('L', 39, 20)]
    [InlineData('N', 39, 18)]
    [InlineData('P', 39, 2)]
    public void Ausgaenge_in_der_Randmauer_liegen_auf_allen_Graden_an_der_Position_der_Rohdaten(char letter, int x, int y)
    {
        for (var level = 1; level <= 5; level++)
        {
            var cave = Caves.Get($"cave-{letter}-{level}");
            Assert.Equal(Element.EscapeDoor, cave.GetElement(x, y));
        }
    }

    private static int Count(CaveData cave, Element element)
    {
        var count = 0;
        for (var i = 0; i < cave.Tiles.Length; i++)
        {
            if ((Element)cave.Tiles[i] == element)
            {
                count++;
            }
        }

        return count;
    }
}
