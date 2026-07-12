using BoulderDash.Core.Data;
using BoulderDash.Core.Flow;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

public class GameSessionTests
{
    private static readonly string CavesPath = Path.Combine(TestPaths.GameAssets, "Caves");

    /// <summary>Session am Option-Screen (Menu): die Session startet auf dem BD1-Titelbildschirm,
    /// erst eine beliebige Taste öffnet das Menü — die meisten Tests setzen dahinter auf.</summary>
    private static GameSession NewRealSession()
    {
        var session = new GameSession(new CaveTextRepository(CavesPath));
        session.TitleAnyKey();
        return session;
    }

    /// <summary>Session am Option-Screen mit geladener BD1-Demo (für die Attract-Mode-Tests).</summary>
    private static GameSession NewRealSessionWithDemo()
    {
        var demoSteps = DemoTextFile.Load(Path.Combine(TestPaths.GameAssets, "demo.txt"));
        var session = new GameSession(new CaveTextRepository(CavesPath), demoSteps);
        session.TitleAnyKey();
        return session;
    }

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

    /// <summary>Sichert die Aussage der Prüfstand-Cave 6 ab: Der obere Stein liegt auf einem FALLENDEN
    /// Stein, weicht deshalb nicht nach links aus und landet gerade unter sich auf dem unteren. Wäre er
    /// (wie im DOS-Original) abgerollt, hätte ihn die Ziegeltreppe nach links unten kaskadiert.</summary>
    [Fact]
    public void Pruefstand_6_laesst_den_oberen_Stein_gerade_fallen_statt_abzurollen()
    {
        var data = new CaveTextRepository(CavesPath).Get("cave-test-6");
        var cave = TestWorld.NewCave(data);

        for (var scan = 0; scan < 40; scan++)
        {
            cave.NextState();
        }

        // Beide Steine stehen übereinander im rechten Schacht (Spalte 12).
        Assert.Equal(Element.Boulder, cave.GetElement(12, 7));
        Assert.Equal(Element.Boulder, cave.GetElement(12, 8));

        // In der linken Hälfte ist kein Stein gelandet — die Ziegeltreppe blieb ungenutzt.
        for (var y = 0; y < data.Height; y++)
        {
            for (var x = 1; x < 11; x++)
            {
                Assert.NotEqual(Element.Boulder, cave.GetElement(x, y));
            }
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
    public void Titelbildschirm_ist_Startphase_und_beliebige_Taste_oeffnet_das_Menue()
    {
        var session = new GameSession(new CaveTextRepository(CavesPath));

        Assert.Equal(SessionPhase.TitleScreen, session.Phase);

        session.TitleAnyKey();
        Assert.Equal(SessionPhase.Menu, session.Phase);

        // Escape auf dem Option-Screen führt zurück zum Titel.
        session.MenuBack();
        Assert.Equal(SessionPhase.TitleScreen, session.Phase);
    }

    /// <summary>Anwählbar sind alle 16 regulären Caves A-P (Abweichung von BD1, das nur A, E, I, M
    /// anbot) — zyklisch in beide Richtungen, die Intermissions Q-T bleiben ausgespart.</summary>
    [Fact]
    public void Menu_Cave_Auswahl_laeuft_zyklisch_durch_A_bis_P()
    {
        var session = NewRealSession();

        Assert.Equal('A', session.SelectedCaveLetter);

        var vorwaerts = new List<char>();
        for (var i = 0; i < 16; i++)
        {
            session.MenuNextCave();
            vorwaerts.Add(session.SelectedCaveLetter);
        }

        // Nach 16 Schritten wieder auf A: alle Caves einmal durch, keine Intermission dabei.
        Assert.Equal(
            ['B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'A'],
            vorwaerts);

        session.MenuPreviousCave();
        Assert.Equal('P', session.SelectedCaveLetter);
    }

    /// <summary>Die Cave-Auswahl gilt auf jedem Schwierigkeitsgrad — die BD1-Handbuchregel "On
    /// Difficulty Levels 4 and 5, you must start with CAVE A" ist bewusst aufgehoben.</summary>
    [Fact]
    public void Cave_Auswahl_bleibt_auf_Grad_4_und_5_frei()
    {
        var session = NewRealSession();

        session.MenuNextCave();
        session.MenuNextCave();
        Assert.Equal('C', session.SelectedCaveLetter);

        for (var i = 0; i < 4; i++)
        {
            session.MenuUp(); // hoch auf Grad 5
        }

        Assert.Equal(5, session.DifficultyLevel);
        Assert.Equal('C', session.SelectedCaveLetter); // Auswahl bleibt stehen

        session.MenuNextCave();
        Assert.Equal('D', session.SelectedCaveLetter); // und bleibt bedienbar
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

        // Simuliert einen erfolgreichen Ausgang, wie ihn RockfordObject.Interact setzt.
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

    /// <summary>BD1-Quirk: Erreicht Rockford den Ausgang in der Nullsekunde (GameTick lässt ihn dort
    /// noch ziehen), startet die Bonuszählung bei 0 und der Byte-Zähler läuft über — die Zeitanzeige
    /// läuft von 255 herunter und der Spieler kassiert 255 Gratispunkte.</summary>
    [Fact]
    public void Ausgang_in_der_Nullsekunde_laesst_den_Bonuszaehler_ueberlaufen()
    {
        var session = NewRealSession();
        session.MenuStart();

        session.State.IsCaveEnded = true;
        session.State.Stat = 0;
        session.State.AdvanceToNextCave = true;
        session.State.CaveTimeRemaining = 0;
        session.State.Score = 0;

        session.Update(0.001); // erkennt IsCaveEnded -> LevelEndBonus, Zähler läuft über
        Assert.Equal(SessionPhase.LevelEndBonus, session.Phase);
        Assert.Equal(255, session.State.CaveTimeRemaining);

        session.Update(10.0); // Bonuszählung abschließen (255 x 20 ms = 5,1 s)
        Assert.Equal(0, session.State.CaveTimeRemaining);
        Assert.Equal(255, session.State.Score);
    }

    /// <summary>"For the last 10 seconds, the running out of time sound is still played, at higher
    /// speed than normal" (Peter Broadribb, Bonus points sound): Die Bonuszählung läuft durch dieselben
    /// Restsekunden 9..0 wie das Spiel selbst und meldet dort dieselbe Zeitwarnung — nur eben im
    /// 20-ms-Takt statt im Sekundentakt. Vorher (Restzeit über 9) darf sie nicht erklingen.</summary>
    [Fact]
    public void Bonuszaehlung_meldet_die_Zeitwarnung_fuer_die_letzten_zehn_Sekunden()
    {
        var session = NewRealSession();
        session.MenuStart();

        session.State.IsCaveEnded = true;
        session.State.Stat = 0;
        session.State.AdvanceToNextCave = true;
        session.State.CaveTimeRemaining = 20;

        session.Update(0.001);
        Assert.Equal(SessionPhase.LevelEndBonus, session.Phase);

        // Die ersten 10 Bonuspunkte (20 -> 10) zählen ohne Zeitwarnung...
        var warningsAbove9 = 0;
        while (session.State.CaveTimeRemaining > 10)
        {
            session.Update(1.0 / 60.0);
            warningsAbove9 += DrainSoundEvents(session).Count(e => e == SoundEvent.TimeWarning);
        }

        Assert.Equal(0, warningsAbove9);

        // ...die letzten 10 (9 -> 0) jeweils mit — zusammen mit dem Bonus-Sweep.
        var warnings = 0;
        var bonusCounts = 0;
        while (session.Phase == SessionPhase.LevelEndBonus && session.State.CaveTimeRemaining > 0)
        {
            session.Update(1.0 / 60.0);
            var events = DrainSoundEvents(session);
            warnings += events.Count(e => e == SoundEvent.TimeWarning);
            bonusCounts += events.Count(e => e == SoundEvent.BonusCount);
        }

        Assert.Equal(10, bonusCounts);
        Assert.Equal(10, warnings);
    }

    private static List<SoundEvent> DrainSoundEvents(GameSession session)
    {
        var events = session.State.SoundEvents.ToList();
        session.State.SoundEvents.Clear();
        return events;
    }

    /// <summary>Gegenprobe: Bloßer Zeitablauf (kein Ausgang, AdvanceToNextCave==false) löst den
    /// Überlauf NICHT aus — es bleibt bei 0 Bonuspunkten.</summary>
    [Fact]
    public void Zeitablauf_ohne_Ausgang_loest_keinen_Bonusueberlauf_aus()
    {
        var session = NewRealSession();
        session.MenuStart();

        session.State.IsCaveEnded = true;
        session.State.Stat = 0;
        session.State.AdvanceToNextCave = false;
        session.State.CaveTimeRemaining = 0;
        session.State.Score = 0;

        session.Update(0.001);
        Assert.Equal(SessionPhase.LevelEndBonus, session.Phase);
        Assert.Equal(0, session.State.CaveTimeRemaining);

        session.Update(10.0);
        Assert.Equal(0, session.State.Score);
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

        // Der Animationstakt gehört seit dem Objektmodell der Cave (früher die globale wechsel_vier).
        var animationsphaseVorher = session.Cave!.AnimationPhase;
        var clk18Vorher = session.Clocks.Clk18;

        // Mehrere kleine Schritte innerhalb der 1s-Todespause (nicht genug, um sie zu beenden).
        for (var i = 0; i < 5; i++)
        {
            session.Update(0.1);
        }

        Assert.Equal(SessionPhase.DeathPause, session.Phase); // Pause läuft noch
        Assert.True(
            session.Cave!.AnimationPhase != animationsphaseVorher || session.Clocks.Clk18 != clk18Vorher,
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

    /// <summary>BD1-Attract-Mode: bleibt der Titelbildschirm unbedient, startet nach
    /// AttractIdleSeconds automatisch die Demo (immer Cave A auf Level 1).</summary>
    [Fact]
    public void Leerlauf_auf_dem_Titelbildschirm_startet_die_Demo()
    {
        var demoSteps = DemoTextFile.Load(Path.Combine(TestPaths.GameAssets, "demo.txt"));
        var session = new GameSession(new CaveTextRepository(CavesPath), demoSteps);
        Assert.Equal(SessionPhase.TitleScreen, session.Phase);

        Pump(session, GameSession.AttractIdleSeconds + 0.5);

        Assert.Equal(SessionPhase.DemoPlaying, session.Phase);
        Assert.Equal('A', session.CurrentCaveData!.Letter);
    }

    [Fact]
    public void Menue_Eingabe_setzt_den_Leerlauf_Timer_zurueck()
    {
        var session = NewRealSessionWithDemo();

        Pump(session, GameSession.AttractIdleSeconds * 0.8);
        session.MenuNextCave();
        Pump(session, GameSession.AttractIdleSeconds * 0.8);

        Assert.Equal(SessionPhase.Menu, session.Phase); // Eingabe hat den Zähler zurückgesetzt

        Pump(session, GameSession.AttractIdleSeconds * 0.4);
        Assert.Equal(SessionPhase.DemoPlaying, session.Phase);
    }

    [Fact]
    public void Taste_waehrend_der_Demo_fuehrt_ueber_das_Zudecken_zum_Titelbildschirm()
    {
        var session = NewRealSessionWithDemo();
        session.StartDemo();
        Assert.Equal(SessionPhase.DemoPlaying, session.Phase);

        Pump(session, 1.0); // die Demo kurz laufen lassen
        session.DemoInterrupted();
        Assert.Equal(SessionPhase.ScreenCovering, session.Phase);

        AdvanceThroughCovering(session);
        session.Update(10.0); // Übergangspause abschließen

        Assert.Equal(SessionPhase.TitleScreen, session.Phase);
        Assert.Null(session.Cave);
    }

    /// <summary>Die Menü-Auswahl lebt getrennt von der Spielposition (_menuCaveSlot) und
    /// übersteht einen Demo-Lauf (der immer Cave A lädt) unverändert.</summary>
    [Fact]
    public void Demo_laesst_die_Menue_Cave_Auswahl_unangetastet()
    {
        var session = NewRealSessionWithDemo();
        session.MenuNextCave();
        Assert.Equal('B', session.SelectedCaveLetter);

        session.StartDemo();
        Assert.Equal(SessionPhase.DemoPlaying, session.Phase);
        session.DemoInterrupted();
        AdvanceThroughCovering(session);
        session.Update(10.0);

        Assert.Equal(SessionPhase.TitleScreen, session.Phase);
        Assert.Equal('B', session.SelectedCaveLetter);
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
                Colors = [new(0x20, 0x20, 0x20), new(0xFF, 0xFF, 0xFF), new(0xBA, 0x20, 0x20), new(0x71, 0xFF, 0xFF)],
                EnchantedWallSeconds = 0, AmoebaSlowGrowthSeconds = 0,
                PointsPerJewelBeforeQuota = 10, PointsPerJewelAfterQuota = 20,
                GameSpeed = CaveSpeed.For(1, isIntermission: false), Tiles = blankTiles,
            };
            _valid = new CaveData
            {
                Index = 1, Name = "Valid", Description = "", Letter = 'B', IsIntermission = false,
                Width = 20, Height = 12, JewelQuota = 0, TimeSeconds = 99,
                Colors = [new(0x20, 0x20, 0x20), new(0xFF, 0xFF, 0xFF), new(0xBA, 0x20, 0x20), new(0x71, 0xFF, 0xFF)],
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
        session.TitleAnyKey();
        session.MenuStart(); // Menü-Auswahl steht auf Cave A (blank)

        Assert.Equal(SessionPhase.Playing, session.Phase);
        Assert.Equal(1, session.CaveIndex); // auf die gültige Cave übergesprungen (Cave B)
    }

    /// <summary>Spielflächen-Zoom auf die volle Cave-Größe: das Sichtfenster zeigt alles, die Kamera
    /// steht damit zwangsläufig in der Ecke — und der Spielzustand bleibt davon unberührt.</summary>
    [Fact]
    public void SetViewport_klemmt_die_Kamera_mitten_im_Spiel_neu()
    {
        var session = NewRealSession();
        session.MenuStart();
        var score = session.State.Score;
        session.Camera.ResetTo(15, 8); // als wäre schon zu Rockford gescrollt worden

        session.SetViewport(new ViewportSize(40, 22));

        Assert.Equal(0, session.Camera.X);
        Assert.Equal(0, session.Camera.Y);
        Assert.Equal(SessionPhase.Playing, session.Phase);
        Assert.Equal(score, session.State.Score);
    }

    /// <summary>Herauszoomen rückt Rockford in die Mitte des neuen Sichtfensters, statt das Fenster nur
    /// nach rechts und unten wachsen zu lassen — sonst driftete er an den Rand. Gewählt ist die große
    /// Prüfstand-Cave, weil in ihr überhaupt Platz ist, mittig zu stehen: Bei einer 40x22-Cave klemmt
    /// die Kamera in jedem Sichtfenster ab 40 Spalten ohnehin auf 0.</summary>
    [Fact]
    public void SetViewport_rueckt_Rockford_in_die_Mitte()
    {
        var session = StartBigTestCave();

        // Bis Rockford aus dem Eingang gestiegen ist (der Aufbau läuft schon während des Aufdeckens).
        for (var frame = 0; frame < 900 && session.Cave!.FindRockford() is null; frame++)
        {
            session.Update(1.0 / 60.0);
        }

        var rockford = session.Cave!.FindRockford();
        Assert.NotNull(rockford);

        var viewport = new ViewportSize(80, 44);
        session.SetViewport(viewport);

        AssertCentred(session, rockford.Index, viewport);
    }

    /// <summary>Steht Rockford noch nicht auf dem Feld (Eingangsaufbau) oder nicht mehr (nach seinem
    /// Tod), zentriert der Zoom den Eingang — dieselbe Stelle wie beim Cave-Start.</summary>
    [Fact]
    public void SetViewport_zentriert_den_Eingang_solange_Rockford_fehlt()
    {
        var session = StartBigTestCave();
        Assert.Null(session.Cave!.FindRockford()); // der Eingang platzt erst später auf

        var viewport = new ViewportSize(80, 44);
        session.SetViewport(viewport);

        AssertCentred(session, session.Cave.FindFirstIndexOf(Element.Entrance), viewport);
    }

    /// <summary>Die große Prüfstand-Cave (400x400) im Spiel. Sie ist für die Zoom-Tests die richtige,
    /// weil in ihr überhaupt Platz ist, mittig zu stehen: Bei einer 40x22-Cave klemmt die Kamera in
    /// jedem Sichtfenster ab 40 Spalten ohnehin auf 0.</summary>
    private static GameSession StartBigTestCave()
    {
        var session = NewRealSession();
        session.MenuTestMode();
        session.TestMenuSelect(GameSession.TestCaves.Count - 1); // cave-test-14, 400x400
        session.TestMenuStart();

        Assert.Equal(SessionPhase.Playing, session.Phase);
        return session;
    }

    /// <summary>Die Kachel steht mittig im Sichtfenster — geklemmt am Cave-Rand (Camera.CenterOn); der
    /// Eingang der großen Prüfstand-Cave liegt links, waagerecht klemmt es dort also.</summary>
    private static void AssertCentred(GameSession session, int index, ViewportSize viewport)
    {
        var cave = session.Cave!;

        Assert.Equal(
            Math.Clamp((index % cave.Width) - (viewport.Columns / 2), 0, cave.Width - viewport.Columns),
            session.Camera.X);
        Assert.Equal(
            Math.Clamp((index / cave.Width) - (viewport.Rows / 2), 0, cave.Height - viewport.Rows),
            session.Camera.Y);
        Assert.Equal(178, session.Camera.Y); // senkrecht ist Platz: hier zentriert es wirklich
    }

    /// <summary>Der Eingang liegt beim Cave-Start mittig im Sichtfenster — auch in einem größeren.</summary>
    [Fact]
    public void Cave_Start_zentriert_den_Eingang_im_gewaehlten_Sichtfenster()
    {
        var viewport = new ViewportSize(24, 14);
        var session = NewRealSession();
        session.SetViewport(viewport);

        session.MenuStart();

        var entrance = session.Cave!.FindFirstIndexOf(Element.Entrance);
        var entranceCol = entrance % session.Cave.Width;
        var entranceRow = entrance / session.Cave.Width;

        Assert.Equal(
            Math.Clamp(entranceCol - (viewport.Columns / 2), 0, session.Cave.Width - viewport.Columns),
            session.Camera.X);
        Assert.Equal(
            Math.Clamp(entranceRow - (viewport.Rows / 2), 0, session.Cave.Height - viewport.Rows),
            session.Camera.Y);
    }
}
