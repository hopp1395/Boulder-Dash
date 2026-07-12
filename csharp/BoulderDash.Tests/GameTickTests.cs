using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class GameTickTests
{
    private const byte Wall = 5;

    private static CaveData BuildCaveData(int width, int height, byte[] tiles) => new()
    {
        Index = 0,
        Name = "Test",
        Description = "",
        Letter = 'A',
        IsIntermission = false,
        Width = (byte)width,
        Height = (byte)height,
        JewelQuota = 0,
        TimeSeconds = 99,
        Colors = [new(0x20, 0x20, 0x20), new(0xFF, 0xFF, 0xFF), new(0xBA, 0x20, 0x20), new(0x71, 0xFF, 0xFF)],
        EnchantedWallSeconds = 0,
        AmoebaSlowGrowthSeconds = 0,
        PointsPerJewelBeforeQuota = 10,
        PointsPerJewelAfterQuota = 20,
        GameSpeed = CaveSpeed.For(1, isIntermission: false),
        Tiles = tiles,
    };

    [Fact]
    public void Eingang_erzeugt_Explosion_bei_92_und_Rockford_bei_99()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 10, 0, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var random = new Random(1);
        var cave = TestWorld.NewCave(BuildCaveData(5, 3, tiles), random);
        var state = cave.State;
        var entranceIndex = cave.FindFirstIndexOf(Element.Entrance);
        var clocks = new Clocks();
        var tick = TestWorld.NewTick(random);

        // Eintretend mit EntranceProgress==92 löst die Explosion aus -> 93 Ticks nötig
        // (Tick 1 bringt EntranceProgress von 0 auf 1, ..., Tick 93 sieht beim Eintritt 92).
        for (var i = 0; i < 93; i++)
        {
            tick.Tick(cave, clocks, entranceIndex);
        }

        Assert.Equal(93, state.EntranceProgress);
        Assert.Equal(Element.Explosion, cave.GetElement(entranceIndex % 5, entranceIndex / 5));

        // Analog: EntranceProgress==99 beim Eintritt löst den Rockford-Spawn aus -> 100 Ticks gesamt.
        for (var i = 93; i < 100; i++)
        {
            tick.Tick(cave, clocks, entranceIndex);
        }

        Assert.Equal(100, state.EntranceProgress);
        Assert.Equal(Element.Rockford, cave.GetElement(entranceIndex % 5, entranceIndex / 5));
    }

    /// <summary>Levelaufbau nach Spezifikation: Erst wenn die Höhle komplett aufgedeckt ist und das
    /// Startsignal (die Eingangs-Explosion bei EntranceProgress==92) ertönt ist, beginnen Steine zu
    /// fallen — vorher hängt der Boulder unbewegt über dem Leerraum.</summary>
    [Fact]
    public void Physik_startet_erst_nach_Aufdecken_und_Startsignal()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 10, 2, 0, Wall,
            Wall, 0, 0, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var data = BuildCaveData(5, 4, tiles);
        var random = new Random(1);
        var cave = TestWorld.NewCave(data, random);
        var entranceIndex = cave.FindFirstIndexOf(Element.Entrance);
        var clocks = new Clocks();
        var cover = new ScreenCover(random);
        cover.BeginUncover(data.Width, data.Height);
        var tick = TestWorld.NewTick(random, cover);

        // Während des Aufdeckens (69 Runden) und bis zum Startsignal (Tick 93 sieht beim Eintritt
        // EntranceProgress==92) rührt sich der Stein nicht.
        for (var i = 0; i < 93; i++)
        {
            tick.Tick(cave, clocks, entranceIndex);
            Assert.Equal(Element.Boulder, cave.GetElement(2, 1));
        }

        // Der nächste Physik-Scan (spätestens 3 Ticks später) lässt ihn fallen.
        for (var i = 0; i < 3; i++)
        {
            tick.Tick(cave, clocks, entranceIndex);
        }

        Assert.Equal(Element.Empty, cave.GetElement(2, 1));
        Assert.Equal(Element.Boulder, cave.GetElement(2, 2));
    }

    /// <summary>Gegenstück zum Aufdecken: Auch während des ZUDECKENS am Cave-Ende ruht die Physik —
    /// der Stein bleibt hängen, bis die Stahlwand durch ist (hier läuft die Cave danach zwar nicht
    /// weiter, der Tick beweist aber, dass es allein an der Zudeckung liegt).</summary>
    [Fact]
    public void Physik_ruht_auch_waehrend_des_Zudeckens()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 6, 2, 0, Wall,
            Wall, 0, 0, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var data = BuildCaveData(5, 4, tiles);
        var random = new Random(1);
        var cave = TestWorld.NewCave(data, random);
        cave.State.EntranceProgress = 101; // Eingang längst fertig, Cave lief bereits
        var clocks = new Clocks();
        var cover = new ScreenCover(random);
        cover.BeginUncover(data.Width, data.Height);
        cover.BeginCover(); // Cave-Ende: die Stahlwand schiebt sich wieder darüber
        var tick = TestWorld.NewTick(random, cover);

        for (var i = 0; i < ScreenCover.Iterations; i++)
        {
            tick.Tick(cave, clocks, entranceIndex: 0);
            Assert.Equal(Element.Boulder, cave.GetElement(2, 1)); // kein Stein fällt
        }

        // Erst nach der letzten Zudeck-Runde (Phase wieder Idle) greift die Physik.
        Assert.False(cover.IsActive);
        for (var i = 0; i < 3; i++)
        {
            tick.Tick(cave, clocks, entranceIndex: 0);
        }

        Assert.Equal(Element.Boulder, cave.GetElement(2, 2));
    }

    /// <summary>Baut Rockford direkt vor den offenen Ausgang (Quote 0), mit fertigem Eingang und
    /// genau einer verbleibenden Spielsekunde.</summary>
    private static (Cave Cave, GameState State, GameTick Tick, Clocks Clocks) SetupLetzteSekundeVorDemAusgang()
    {
        byte[] tiles =
        [
            Wall, Wall, Wall, Wall, Wall,
            Wall, 6, 11, 0, Wall,
            Wall, Wall, Wall, Wall, Wall,
        ];
        var random = new Random(1);
        var cave = TestWorld.NewCave(BuildCaveData(5, 3, tiles), random);
        cave.State.EntranceProgress = 101; // Eingang fertig, Rockford lebt
        cave.State.CaveTimeRemaining = 1;
        return (cave, cave.State, TestWorld.NewTick(random), new Clocks());
    }

    /// <summary>BD1-Quirk (Vorbedingung für den Bonusüberlauf in GameSession.BeginLevelEndBonus): Die
    /// Nullsekunde wird noch ausgespielt — die Anzeige steht auf 000, die Cave läuft aber weiter, und
    /// Rockford kann in dieser Sekunde noch in den Ausgang ziehen. Das DOS-Original beendete die Cave
    /// sofort bei 0 (BOULDER.CPP:251) und kannte den Quirk deshalb nicht.</summary>
    [Fact]
    public void Ausgang_ist_in_der_Nullsekunde_noch_erreichbar()
    {
        var (cave, state, tick, clocks) = SetupLetzteSekundeVorDemAusgang();

        // Bis die Zeit auf 000 fällt — ohne Eingabe, Rockford bleibt vor dem Ausgang stehen.
        for (var i = 0; i < 200 && state.CaveTimeRemaining > 0; i++)
        {
            tick.Tick(cave, clocks, entranceIndex: 0);
        }

        Assert.Equal(0, state.CaveTimeRemaining);
        Assert.False(state.IsCaveEnded); // Gnadensekunde: die Cave läuft noch

        // Jetzt erst in den Ausgang ziehen (Physik läuft jeden 3. Tick).
        cave.Input.PressRight();
        for (var i = 0; i < 3; i++)
        {
            tick.Tick(cave, clocks, entranceIndex: 0);
        }

        Assert.True(state.IsCaveEnded);
        Assert.True(state.AdvanceToNextCave); // Ausgang erreicht, kein bloßer Zeitablauf
    }

    /// <summary>Gegenprobe: Wer die Nullsekunde verstreichen lässt, verliert die Cave am folgenden
    /// Sekundentakt — der Zeitablauf beendet sie ohne Fortschritt zur nächsten Cave.</summary>
    [Fact]
    public void Nullsekunde_beendet_die_Cave_am_folgenden_Sekundentakt()
    {
        // Keine Eingabe: Rockford bleibt stehen.
        var (cave, state, tick, clocks) = SetupLetzteSekundeVorDemAusgang();

        var ticks = 0;
        for (; ticks < 200 && !state.IsCaveEnded; ticks++)
        {
            tick.Tick(cave, clocks, entranceIndex: 0);
        }

        Assert.True(state.IsCaveEnded);
        Assert.False(state.AdvanceToNextCave);
        Assert.Equal(0, state.CaveTimeRemaining);

        // Zwei volle Sekundenperioden (letzte Sekunde + Nullsekunde), nicht nur eine.
        Assert.InRange(ticks, Clocks.DefaultGameSecondTicks + 1, 2 * Clocks.DefaultGameSecondTicks);
    }
}
