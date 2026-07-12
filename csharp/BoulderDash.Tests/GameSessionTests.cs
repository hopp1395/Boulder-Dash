using BoulderDash.Core.Data;
using BoulderDash.Core.Flow;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class GameSessionTests
{
    private static readonly string CavesPath = Path.Combine(TestPaths.GameAssets, "Caves");

    private static GameSession NewRealSession() => new(new CaveTextRepository(CavesPath));

    /// <summary>Pumpt die Session in Frame-Schritten durch die Zudeck-Animation am Cave-Ende
    /// (ScreenCovering: 69 Runden, eine pro Tick — mehr, als AdvanceSimulation pro Update-Aufruf
    /// zulässt).</summary>
    private static void AdvanceThroughCovering(GameSession session)
    {
        for (var frame = 0; frame < 600 && session.Phase == SessionPhase.ScreenCovering; frame++)
        {
            session.Update(1.0 / 60.0);
        }
    }

    /// <summary>F5 öffnet den Testmodus; von dort startet jede Prüfstand-Cave einzeln. Sie stehen
    /// außerhalb der PlayOrder — ein Escape führt daher zurück in den Testmodus und nicht in eine
    /// Cave der Spielreihenfolge.</summary>
    [Fact]
    public void Testmodus_startet_jede_Pruefstand_Cave_und_kehrt_danach_dorthin_zurueck()
    {
        var session = NewRealSession();

        session.MenuTestMode();
        Assert.Equal(SessionPhase.TestMenu, session.Phase);

        for (var i = 0; i < GameSession.TestCaves.Count; i++)
        {
            session.TestMenuSelect(i);
            session.TestMenuStart();

            Assert.Equal(SessionPhase.Playing, session.Phase);
            Assert.NotNull(session.CurrentCaveData);

            session.EscapePressed();
            AdvanceThroughCovering(session);
            for (var frame = 0; frame < 120 && session.Phase != SessionPhase.TestMenu; frame++)
            {
                session.Update(1.0 / 60.0);
            }

            Assert.Equal(SessionPhase.TestMenu, session.Phase);
            Assert.Null(session.Cave);
        }
    }

    [Fact]
    public void Menu_Cave_Auswahl_ist_auf_A_bis_P_begrenzt()
    {
        var session = NewRealSession();

        for (var i = 0; i < 30; i++)
        {
            session.MenuNextCave();
        }

        Assert.Equal('P', session.SelectedCaveLetter);

        for (var i = 0; i < 30; i++)
        {
            session.MenuPreviousCave();
        }

        Assert.Equal('A', session.SelectedCaveLetter);
    }

    [Fact]
    public void Schwierigkeitsgrad_ist_auf_1_bis_5_begrenzt()
    {
        var session = NewRealSession();

        for (var i = 0; i < 10; i++)
        {
            session.MenuUp();
        }

        Assert.Equal(5, session.DifficultyLevel);

        for (var i = 0; i < 10; i++)
        {
            session.MenuDown();
        }

        Assert.Equal(1, session.DifficultyLevel);
    }

    [Fact]
    public void F1_startet_Session_mit_3_Chancen_und_geht_in_Playing()
    {
        var session = NewRealSession();
        session.State.Chances = 1; // simuliert Rest einer vorherigen (verlorenen) Session

        session.MenuStart();

        Assert.Equal(SessionPhase.Playing, session.Phase);
        Assert.Equal(3, session.State.Chances);
        Assert.NotNull(session.Cave);
        Assert.Equal('A', session.CurrentCaveData!.Letter);
    }

    [Fact]
    public void Escape_waehrend_des_Spiels_fuehrt_nach_der_Uebergangspause_zurueck_ins_Menue()
    {
        var session = NewRealSession();
        session.MenuStart();

        session.EscapePressed();
        Assert.Equal(SessionPhase.ScreenCovering, session.Phase); // erst deckt sich der Bildschirm zu

        AdvanceThroughCovering(session);
        Assert.Equal(SessionPhase.CaveTransition, session.Phase);

        session.Update(10.0); // Übergangspause (0,5s) sicher überschreiten

        Assert.Equal(SessionPhase.Menu, session.Phase);
        Assert.Null(session.Cave);
    }

    [Fact]
    public void Erfolgreicher_Ausgang_fuehrt_ueber_Bonuszaehlung_und_Uebergang_zur_naechsten_Cave()
    {
        var session = NewRealSession();
        session.MenuStart();

        // Simuliert einen erfolgreichen Ausgang, wie ihn CavePhysics.ProcessRockford setzt.
        session.State.IsCaveEnded = true;
        session.State.Stat = 0;
        session.State.AdvanceToNextCave = true;
        session.State.CaveTimeRemaining = 5;

        session.Update(0.001); // erkennt IsCaveEnded -> LevelEndBonus
        Assert.Equal(SessionPhase.LevelEndBonus, session.Phase);

        session.Update(10.0); // Bonuszählung + Nachpause sicher abschließen
        Assert.Equal(SessionPhase.ScreenCovering, session.Phase);

        AdvanceThroughCovering(session);
        Assert.Equal(SessionPhase.CaveTransition, session.Phase);

        session.Update(10.0); // Übergangspause abschließen -> nächste Cave (B)
        Assert.Equal(SessionPhase.Playing, session.Phase);
        Assert.Equal('B', session.CurrentCaveData!.Letter);
    }

    [Fact]
    public void Bewegungsrichtung_wird_beim_Cave_Wechsel_zurueckgesetzt()
    {
        // Regressionstest: Ohne Reset bewegte sich Rockford in der neuen Cave automatisch in die
        // Richtung weiter, in der zuvor der Ausgang der alten Cave betreten wurde (Nutzer-Feedback:
        // "bewegt sich rockford in die gleiche richtung ... automatisch ohne eine taste zu drücken").
        var session = NewRealSession();
        session.MenuStart();
        session.Input.PressRight(); // simuliert: Taste beim Betreten des Ausgangs noch gehalten

        session.State.IsCaveEnded = true;
        session.State.Stat = 0;
        session.State.AdvanceToNextCave = true;
        session.State.CaveTimeRemaining = 0;

        session.Update(0.001);
        Assert.Equal(SessionPhase.LevelEndBonus, session.Phase);

        session.Update(10.0); // Nachpause abschließen -> ScreenCovering
        AdvanceThroughCovering(session); // Zudecken -> CaveTransition
        session.Update(10.0); // Übergangspause abschließen -> nächste Cave (B), Playing

        Assert.Equal(SessionPhase.Playing, session.Phase);
        Assert.Equal(0, session.Input.Direction);
        Assert.Equal(0, session.Input.Flags);
    }

    [Fact]
    public void Tod_kostet_ein_Leben_und_laedt_dieselbe_Cave_erneut()
    {
        var session = NewRealSession();
        session.MenuStart();
        var chancesVorher = session.State.Chances;

        // Ein Tod durch einen fallenden Stein setzt NUR stat, nicht level_ende (BOULDER.CPP:
        // game_start() prüft beide als unabhängige Abbruchbedingungen, :397-398) — IsCaveEnded
        // bewusst NICHT gesetzt, um genau diesen Fall (vormals ein Bug: Playing blieb ewig hängen)
        // abzudecken.
        session.State.Stat = 1; // Rockford nicht mehr gefunden -> tot

        session.Update(0.001);
        Assert.Equal(SessionPhase.DeathPause, session.Phase);
        Assert.Equal(chancesVorher - 1, session.State.Chances);

        session.Update(10.0); // delay(1000) überschreiten
        session.AnyKeyPressed();
        Assert.Equal(SessionPhase.ScreenCovering, session.Phase);

        AdvanceThroughCovering(session);
        session.Update(10.0);
        Assert.Equal(SessionPhase.Playing, session.Phase);
        Assert.Equal('A', session.CurrentCaveData!.Letter); // dieselbe Cave, kein Fortschritt
    }

    [Fact]
    public void Animation_laeuft_waehrend_der_Todes_Pause_weiter_statt_einzufrieren()
    {
        // Im Original bleibt die Timer-ISR bis NACH den delay()/getch()-Aufrufen aktiv
        // (Init_ISR(DEINSTALL) steht ganz am Ende von game_start(), BOULDER.CPP:421) — Animation
        // (und bei einem Tod ohne level_ende auch die Physik) läuft während der Todes-Pause im
        // Original also weiter. Vormals ein Bug: das Spiel wirkte während der Pause eingefroren.
        var session = NewRealSession();
        session.MenuStart();
        session.State.Stat = 1;
        session.Update(0.001);
        Assert.Equal(SessionPhase.DeathPause, session.Phase);

        var wechselVierVorher = session.State.WechselVier;
        var clk18Vorher = session.Clocks.Clk18;

        // Mehrere kleine Schritte innerhalb der 1s-Todespause (nicht genug, um sie zu beenden).
        for (var i = 0; i < 5; i++)
        {
            session.Update(0.1);
        }

        Assert.Equal(SessionPhase.DeathPause, session.Phase); // Pause läuft noch
        Assert.True(
            session.State.WechselVier != wechselVierVorher || session.Clocks.Clk18 != clk18Vorher,
            "Animationszähler haben sich während der Todes-Pause nicht bewegt — Spiel wirkt eingefroren.");
    }

    [Fact]
    public void Game_Over_bei_0_Chancen_fuehrt_zurueck_ins_Menue()
    {
        var session = NewRealSession();
        session.MenuStart();
        session.State.Chances = 1; // letztes Leben

        session.State.Stat = 1; // Tod ohne IsCaveEnded, siehe voriger Test

        session.Update(0.001);
        Assert.Equal(0, session.State.Chances);
        Assert.Equal(SessionPhase.DeathPause, session.Phase);

        session.Update(10.0); // erste Pause -> GameOverMessage
        Assert.Equal(SessionPhase.GameOverMessage, session.Phase);
        Assert.True(session.ShowGameOverMessage);

        session.Update(10.0); // zweite Pause
        session.AnyKeyPressed();
        Assert.Equal(SessionPhase.ScreenCovering, session.Phase);

        AdvanceThroughCovering(session);
        session.Update(10.0);
        Assert.Equal(SessionPhase.Menu, session.Phase);
    }

    [Fact]
    public void Cave_startet_vollstaendig_verdeckt_und_ist_nach_dem_Aufdecken_frei()
    {
        // BD1: die Cave liegt zu Beginn komplett unter der animierten Stahlwand und wird
        // zeilenweise-zufällig freigelegt (69 Runden, eine pro Tick).
        var session = NewRealSession();
        session.MenuStart();

        var cave = session.Cave!;
        Assert.True(session.ScreenCover.IsActive);
        Assert.True(session.ScreenCover.IsCovered(0, 0));
        Assert.True(session.ScreenCover.IsCovered(cave.Width - 1, cave.Height - 1));

        for (var frame = 0; frame < 600 && session.ScreenCover.IsActive; frame++)
        {
            session.Update(1.0 / 60.0);
        }

        Assert.False(session.ScreenCover.IsActive);
        Assert.False(session.ScreenCover.IsCovered(0, 0));
        Assert.Equal(SessionPhase.Playing, session.Phase);
    }

    [Fact]
    public void Cave_Ende_deckt_den_Bildschirm_wieder_zu()
    {
        var session = NewRealSession();
        session.MenuStart();

        session.EscapePressed();
        AdvanceThroughCovering(session);

        var cave = session.Cave!;
        Assert.Equal(SessionPhase.CaveTransition, session.Phase);
        Assert.True(session.ScreenCover.IsCovered(0, 0));
        Assert.True(session.ScreenCover.IsCovered(cave.Width - 1, cave.Height - 1));
    }

    /// <summary>Startet Cave A auf dem gegebenen Schwierigkeitsgrad.</summary>
    private static GameSession StartedAtLevel(int level)
    {
        var session = NewRealSession();
        for (var i = 1; i < level; i++)
        {
            session.MenuUp();
        }

        Assert.Equal(level, session.DifficultyLevel);
        session.MenuStart();
        return session;
    }

    /// <summary>Füttert die Session mit echter Zeit in 60-Hz-Frames.</summary>
    private static void Pump(GameSession session, double seconds)
    {
        for (var frame = 0; frame < (int)Math.Round(seconds * 60); frame++)
        {
            session.Update(1.0 / 60.0);
        }
    }

    /// <summary>BD1: das Tempo hängt am Schwierigkeitsgrad (CaveDelay 12/6/3/1/0). EntranceProgress
    /// zählt genau einen Tick pro Tick und ist deshalb ein direktes Maß für die Tickrate.</summary>
    [Fact]
    public void Hoeherer_Schwierigkeitsgrad_laesst_die_Cave_schneller_laufen()
    {
        var grad1 = StartedAtLevel(1);
        var grad5 = StartedAtLevel(5);

        Pump(grad1, 2.0);
        Pump(grad5, 2.0);

        Assert.Equal(40, grad1.State.EntranceProgress); // 2 s / 50 ms
        Assert.True(
            grad5.State.EntranceProgress > grad1.State.EntranceProgress * 1.3,
            $"Grad 5 muss deutlich schneller ticken als Grad 1 (war {grad5.State.EntranceProgress} vs. {grad1.State.EntranceProgress}).");
    }

    /// <summary>Gegenstück dazu: der Zeit-Countdown zählt in BD1 IRQ-getrieben und ist damit
    /// tempo-UNabhängig — eine Spielsekunde dauert bei jedem Grad ~1,1 reale Sekunden (CaveSpeed).
    /// Ohne die nachgeführte Clk18-Periode würde Grad 5 hier ~14 statt ~10 Sekunden verbrauchen.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Zeit_Countdown_laeuft_unabhaengig_vom_Schwierigkeitsgrad(int level)
    {
        var session = StartedAtLevel(level);

        // Der Countdown startet erst nach dem Aufbau des Eingangs (EntranceProgress > 99).
        for (var frame = 0; frame < 1000 && session.State.EntranceProgress <= 99; frame++)
        {
            session.Update(1.0 / 60.0);
        }

        var vorher = session.State.CaveTimeRemaining;
        Pump(session, 11.0);
        var verbraucht = vorher - session.State.CaveTimeRemaining;

        Assert.Equal(SessionPhase.Playing, session.Phase);
        Assert.InRange(verbraucht, 9, 11); // 11 s Realzeit / 1,1 s pro Spielsekunde = 10
    }

    /// <summary>Test-Repository, das für Cave A eine Karte ohne Eingang liefert und für alle
    /// anderen Caves eine gültige Karte - deckt das Sicherheitsnetz in LoadCaveWithSkip ab.</summary>
    private sealed class BlankFirstCaveRepository : ICaveRepository
    {
        private readonly CaveData _blank;
        private readonly CaveData _valid;

        public BlankFirstCaveRepository()
        {
            byte[] blankTiles = new byte[40 * 22]; // alles 0, kein Eingang
            byte[] validTiles = new byte[20 * 12];
            // Minimaler gültiger 20x12-Rand mit Eingang und Ausgang.
            for (var x = 0; x < 20; x++)
            {
                validTiles[x] = 5;
                validTiles[(11 * 20) + x] = 5;
            }

            for (var y = 0; y < 12; y++)
            {
                validTiles[y * 20] = 5;
                validTiles[(y * 20) + 19] = 5;
            }

            validTiles[(1 * 20) + 1] = 10; // Eingang
            validTiles[(1 * 20) + 2] = 11; // Ausgang

            _blank = new CaveData
            {
                Index = 0, Name = "Blank", Description = "", Letter = 'A', IsIntermission = false,
                Width = 40, Height = 22, JewelQuota = 0, TimeSeconds = 99,
                BaseColors = [0, 1, 2, 3], CameraStartX = 0, CameraStartY = 0,
                EnchantedWallSeconds = 0, AmoebaSlowGrowthSeconds = 0,
                PointsPerJewelBeforeQuota = 10, PointsPerJewelAfterQuota = 20,
                GameSpeed = CaveSpeed.For(1, isIntermission: false), Tiles = blankTiles,
            };
            _valid = new CaveData
            {
                Index = 1, Name = "Valid", Description = "", Letter = 'B', IsIntermission = false,
                Width = 20, Height = 12, JewelQuota = 0, TimeSeconds = 99,
                BaseColors = [0, 1, 2, 3], CameraStartX = 0, CameraStartY = 0,
                EnchantedWallSeconds = 0, AmoebaSlowGrowthSeconds = 0,
                PointsPerJewelBeforeQuota = 10, PointsPerJewelAfterQuota = 20,
                GameSpeed = CaveSpeed.For(1, isIntermission: false), Tiles = validTiles,
            };
        }

        public CaveData Get(string name) => name.StartsWith("cave-A-", StringComparison.OrdinalIgnoreCase) ? _blank : _valid;
    }

    [Fact]
    public void Leere_Platzhalter_Cave_wird_beim_Laden_uebersprungen()
    {
        var session = new GameSession(new BlankFirstCaveRepository());
        session.MenuStart(); // CaveIndex steht auf 0 (Cave A, blank)

        Assert.Equal(SessionPhase.Playing, session.Phase);
        Assert.Equal(1, session.CaveIndex); // auf die gültige Cave übergesprungen (Cave B)
    }
}
