using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Flow;

public enum SessionPhase
{
    /// <summary>Reiner BD1-Titelbildschirm (Logo + First-Star-Schriftzug) — der Anfangszustand.
    /// Eine beliebige Taste öffnet den Option-Screen (Menu); nach Leerlauf startet die Demo
    /// (Attract-Mode). Ersetzt zusammen mit Menu das DOS-Menü (BD1-Ausnahme, siehe CLAUDE.md).</summary>
    TitleScreen,

    /// <summary>BD1-Option-Screen: Logo plus Textzeilen mit der CAVE/LEVEL-Auswahl
    /// ("PRESS SPACE TO PLAY").</summary>
    Menu,

    /// <summary>Auswahl der Prüfstand-Caves (F5 im Hauptmenü) — kein Original-Zustand, sondern ein
    /// Entwicklerzugang zum Testgelände für das Objektverhalten (siehe GameSession.TestCaves).</summary>
    TestMenu,
    Playing,
    LevelEndBonus,
    DeathPause,
    GameOverMessage,

    /// <summary>Der Bildschirm deckt sich am Cave-Ende wieder mit der animierten Stahlwand zu
    /// (ScreenCover, BD1) — läuft vor jeder CaveTransition, egal aus welchem Grund.</summary>
    ScreenCovering,
    CaveTransition,
    DemoPlaying,
}

/// <summary>
/// Grund, warum gerade eine CaveTransition läuft — bestimmt, was danach passiert
/// (nächste Cave laden, dieselbe erneut laden, oder zurück ins Menü).
/// </summary>
public enum TransitionReason
{
    Success,
    TimeoutAlive,
    DeathRetry,
    GameOver,
    EscQuit,

    /// <summary>Demo endet (Ende der Aufzeichnung, Cave-Ende, Tod oder Tastendruck) — kehrt wie
    /// im BD1-Attract-Zyklus immer zum Titelbildschirm zurück.</summary>
    DemoEnd,
}

/// <summary>
/// Orchestriert Menü, Cave-Ablauf und Progression — Analog zu Start_menu()/game_start()/
/// Level_End() (src/BOULDER.CPP:273-423, GAME.CPP:34-62). Wandelt die im Original blockierenden
/// delay()/getch()-Aufrufe in einen Zustandsautomaten mit Restzeit um (siehe Update()).
///
/// Das DOS-Menü (Start_menu-Marquee, F-Tasten-Legende, Kachelrahmen) ist durch den BD1-Ablauf
/// ersetzt (BD1-Ausnahme wie ScreenCover/Amoeba/CaveSpeed, siehe CLAUDE.md): Titelbildschirm →
/// beliebige Taste → Option-Screen mit CAVE/LEVEL-Auswahl; nach Leerlauf startet automatisch
/// die Demo (Attract-Mode), aus der Tastendruck oder Demo-Ende zum Titel zurückführen.
///
/// Nicht-offensichtliche Original-Regeln, hier bewusst nachgebildet:
/// - Chances (leben) werden NUR beim Spielstart auf 3 zurückgesetzt, nicht pro Cave.
/// - Zeitablauf OHNE Tod (Rockford lebt, Diamanten/Ausgang nicht erreicht) kostet kein Leben
///   und lädt dieselbe Cave erneut (level_next bleibt 0).
/// - Nur ein Ausgangs-Erfolg (AdvanceToNextCave) rückt zur nächsten Cave vor.
/// - Escape während des Spiels beendet die Session und kehrt ins Menü zurück (nicht nur die Cave),
///   durchläuft aber noch die Übergangs-Pause wie jedes andere Cave-Ende.
/// - Die Menü-Cave-Auswahl bietet alle 16 regulären Caves A-P auf jedem Schwierigkeitsgrad an,
///   siehe MenuCaveIndices. Das ist eine bewusste Abweichung von BD1, das nur A, E, I und M
///   zuließ (Handbuch: "You may choose CAVE A, E, I, or M, on Difficulty Levels 1-3. On
///   Difficulty Levels 4 and 5, you must start with CAVE A."). Nur die Intermissions (Q-T)
///   bleiben der Fortschritts-Kette vorbehalten, siehe PlayOrder.
/// </summary>
public sealed class GameSession
{
    /// <summary>Spielreihenfolge der 20 BD1-Caves als Cave-Buchstaben: nach je 4 regulären Caves eine
    /// Intermission (Q-T, Original-BD1-Struktur). CaveIndex indiziert in diese Tabelle.</summary>
    private static readonly char[] PlayOrder =
    [
        'A', 'B', 'C', 'D', 'Q',
        'E', 'F', 'G', 'H', 'R',
        'I', 'J', 'K', 'L', 'S',
        'M', 'N', 'O', 'P', 'T',
    ];

    /// <summary>Eine Prüfstand-Cave: Dateiname im Repository plus die Zeile, die im Testmodus-Menü
    /// erscheint. Der Titel darf höchstens 35 Zeichen haben — der TestMenuRenderer stellt Auswahlmarke und
    /// Nummer voran, und die Textzeile ist 40 Zeichen breit.</summary>
    public readonly record struct TestCave(string Name, string Title);

    /// <summary>Die Prüfstand-Caves (Assets/Caves/cave-test-N.txt) — je eine pro Korrektur am
    /// Objektverhalten, jede mit einer Anleitung im Kopf der Datei. Kein BD1-Inhalt: sie stehen
    /// außerhalb der PlayOrder und sind nur über den Testmodus (F5) erreichbar.</summary>
    public static readonly IReadOnlyList<TestCave> TestCaves =
    [
        new("cave-test-1", "BUTTERFLY ZIEHT ZUERST LINKS"),
        new("cave-test-2", "FIREFLY LINKS-, BUTTERFLY RECHTSRUM"),
        new("cave-test-3", "KREATUR ZUENDET BEI KONTAKT"),
        new("cave-test-4", "SCHIEBEN: 1 ZU 8, UND ES KLINGT"),
        new("cave-test-5", "ZAUBERMAUER WANDELT UM UND KLINGT"),
        new("cave-test-6", "KEIN ABROLLEN VOM FALLENDEN STEIN"),
        new("cave-test-7", "AUSGANG ZAEHLT NICHT ALS DIAMANT"),
        new("cave-test-8", "EXPLOSION VERSCHONT DEN AUSGANG"),
        new("cave-test-9", "HOCHLAUFEN RUCKELT NICHT MEHR"),
        new("cave-test-10", "WAAGERECHT SCHLAEGT SENKRECHT"),
        new("cave-test-11", "AMOEBE: NUR DER LINKE ZUENDET (B)"),
        new("cave-test-12", "AMOEBE: NUR DER LINKE ZUENDET (F)"),
    ];

    /// <summary>Anwählbare Start-Caves als PlayOrder-Indizes: alle 16 regulären Caves A-P, also
    /// die PlayOrder ohne die Intermissions Q-T (Indizes 4, 9, 14, 19). BD1 bot hier nur die vier
    /// Blockanfänge A, E, I, M an; direkt anwählbar sind bei uns alle, damit jede Cave ohne
    /// Durchspielen der vorherigen erreichbar ist.</summary>
    private static readonly sbyte[] MenuCaveIndices =
    [
        0, 1, 2, 3,
        5, 6, 7, 8,
        10, 11, 12, 13,
        15, 16, 17, 18,
    ];

    private const double BonusSecondsPerPoint = 0.02; // delay(20) pro Punkt, GAME.CPP:58
    private const double PostBonusPauseSeconds = 1.0; // delay(1000) nach der Bonuszählung
    private const double DeathPauseSeconds = 1.0; // delay(1000)
    private const double GameOverExtraPauseSeconds = 1.0; // zweites delay(1000) vor GAME OVER
    private const double CaveTransitionPauseSeconds = 0.5; // delay(500) vor level_out()

    /// <summary>Leerlaufzeit auf Titel-/Option-Screen, nach der die Demo anläuft (BD1-Attract-
    /// Mode). Die Original-Wartezeit ist nicht vermessen — 12 s sind eine frei gewählte,
    /// leicht justierbare Annahme.</summary>
    public const double AttractIdleSeconds = 12.0;

    private readonly ICaveRepository _caves;
    private readonly Random _random;
    private readonly ScreenCover _cover;
    private readonly GameTick _gameTick;

    private double _phaseTimer;
    private double _tickAccumulator;
    private double _secondsPerTick;
    private double _idleTimer;

    /// <summary>Wie das Original-`start` (WORD, BOULDER.CPP:112): einmal beim Laden ermittelt und
    /// danach unverändert weiterverwendet — die Eingangskachel wird während des Aufbaus zu
    /// Explosion und dann zu Rockford, ein erneutes Suchen würde ab da fehlschlagen.</summary>
    private int _entranceIndex;
    private TransitionReason _transitionReason;

    private readonly IReadOnlyList<DemoStep>? _demoSteps;
    private DemoPlayer? _demoPlayer;
    private bool _isDemo;
    private bool _isTestCave;

    /// <summary>Die Menü-Auswahl als Index in <see cref="MenuCaveIndices"/> (0-15 = A…P).
    /// Sie lebt getrennt von <see cref="CaveIndex"/> (der laufenden Spielposition) und übersteht
    /// damit Demo-Läufe und die Cave-Progression unverändert.</summary>
    private int _menuCaveSlot;

    public SessionPhase Phase { get; private set; } = SessionPhase.TitleScreen;
    public sbyte CaveIndex { get; private set; }

    /// <summary>Im Testmodus gewählte Prüfstand-Cave (Index in <see cref="TestCaves"/>).</summary>
    public int TestCaveIndex { get; private set; }

    public sbyte DifficultyLevel { get; private set; } = 1;
    public bool QuitRequested { get; private set; }
    public bool ShowGameOverMessage { get; private set; }

    public GameState State { get; }
    public InputState Input { get; }
    public Camera Camera { get; }
    public Clocks Clocks { get; }
    public Simulation.Cave? Cave { get; private set; }
    public CaveData? CurrentCaveData { get; private set; }

    /// <summary>Verdeckungs-Overlay für die Rendering-Schicht — eine Instanz für die gesamte
    /// Session, die sich den Zufallsstrom mit der Physik teilt (wie im Original level_in() und
    /// regel()).</summary>
    public ScreenCover ScreenCover => _cover;

    public GameSession(ICaveRepository caves, IReadOnlyList<DemoStep>? demoSteps = null)
    {
        _caves = caves;
        _demoSteps = demoSteps;
        // Fester Seed: Das Original ruft nie srand(), läuft also bei jedem Start durch dieselbe
        // rand()-Sequenz. Amoeba-Ausbreitung und ScreenCover sind damit reproduzierbar.
        _random = new Random(1);
        _cover = new ScreenCover(_random);
        _gameTick = new GameTick(_cover, _random);

        State = new GameState();
        Input = new InputState();
        Camera = new Camera();
        Clocks = new Clocks();
    }

    /// <summary>Cave-Buchstabe der aktuell im Menü gewählten (nicht zwingend geladenen) Cave.</summary>
    public char SelectedCaveLetter => PlayOrder[MenuCaveIndices[_menuCaveSlot]];

    /// <summary>Name, unter dem eine Cave im Repository liegt (= Dateiname ohne Endung).</summary>
    private static string NameFor(int playIndex, int level) => $"cave-{PlayOrder[playIndex]}-{level}";

    public void Update(double deltaSeconds)
    {
        switch (Phase)
        {
            case SessionPhase.TitleScreen:
            case SessionPhase.Menu:
                UpdateAttractIdle(deltaSeconds);
                break;
            case SessionPhase.Playing:
                UpdatePlaying(deltaSeconds);
                break;
            case SessionPhase.LevelEndBonus:
                UpdateLevelEndBonus(deltaSeconds);
                break;
            case SessionPhase.DeathPause:
                UpdateDeathPause(deltaSeconds);
                break;
            case SessionPhase.GameOverMessage:
                UpdateGameOverMessage(deltaSeconds);
                break;
            case SessionPhase.ScreenCovering:
                UpdateScreenCovering(deltaSeconds);
                break;
            case SessionPhase.CaveTransition:
                UpdateCaveTransition(deltaSeconds);
                break;
            case SessionPhase.DemoPlaying:
                UpdateDemoPlaying(deltaSeconds);
                break;
        }
    }

    /// <summary>BD1-Attract-Mode: bleibt der Titel-/Option-Screen eine Weile unbedient, läuft
    /// die Demo an. Jede Menü-Eingabe setzt den Zähler zurück (ResetIdleTimer).</summary>
    private void UpdateAttractIdle(double deltaSeconds)
    {
        _idleTimer += deltaSeconds;
        if (_idleTimer >= AttractIdleSeconds)
        {
            _idleTimer = 0;
            StartDemo();
        }
    }

    private void ResetIdleTimer() => _idleTimer = 0;

    /// <summary>Beliebige Taste auf dem Titelbildschirm: öffnet den Option-Screen — wie der
    /// Feuerknopf/F1 im BD1-Original ("F1 or fire button: show menu lines").</summary>
    public void TitleAnyKey()
    {
        if (Phase != SessionPhase.TitleScreen) return;
        ResetIdleTimer();
        Phase = SessionPhase.Menu;
    }

    /// <summary>Escape auf dem Option-Screen: zurück zum Titelbildschirm. Kein BD1-Beleg —
    /// macht den reinen Titel ohne Neustart wieder erreichbar.</summary>
    public void MenuBack()
    {
        if (Phase != SessionPhase.Menu) return;
        ResetIdleTimer();
        Phase = SessionPhase.TitleScreen;
    }

    // Menü-Eingaben des Option-Screens: Hoch/Runter = Level, Links/Rechts = Cave, wie die
    // Joystick-Auswahl im BD1-Original (Handbuch: "move the joystick arm left or right/up or
    // down when you are in the menu screen").
    public void MenuUp()
    {
        if (Phase != SessionPhase.Menu) return;
        ResetIdleTimer();
        DifficultyLevel = Math.Min((sbyte)(DifficultyLevel + 1), (sbyte)5);
    }

    public void MenuDown()
    {
        if (Phase != SessionPhase.Menu) return;
        ResetIdleTimer();
        DifficultyLevel = Math.Max((sbyte)(DifficultyLevel - 1), (sbyte)1);
    }

    public void MenuNextCave()
    {
        if (Phase != SessionPhase.Menu) return;
        ResetIdleTimer();
        // Zyklisch A→B→…→P→A — Joystick-Semantik ohne Anschlag (Annahme, im BD1 nicht vermessen).
        _menuCaveSlot = (_menuCaveSlot + 1) % MenuCaveIndices.Length;
    }

    public void MenuPreviousCave()
    {
        if (Phase != SessionPhase.Menu) return;
        ResetIdleTimer();
        _menuCaveSlot = (_menuCaveSlot + MenuCaveIndices.Length - 1) % MenuCaveIndices.Length;
    }

    /// <summary>Escape auf dem Titelbildschirm: Programm beenden. Escape führt damit überall eine
    /// Ebene nach oben (Spiel → Option-Screen → Titel → beenden), siehe <see cref="MenuBack"/>.</summary>
    public void MenuQuit()
    {
        if (Phase != SessionPhase.TitleScreen) return;
        QuitRequested = true;
    }

    /// <summary>Spielstart ("PRESS SPACE TO PLAY"): leben=3, gewählte Menü-Cave laden.</summary>
    public void MenuStart()
    {
        if (Phase != SessionPhase.Menu) return;
        ResetIdleTimer();
        State.Chances = 3;
        _isTestCave = false;
        LoadCaveWithSkip(MenuCaveIndices[_menuCaveSlot]);
    }

    /// <summary>F5: in den Testmodus wechseln (siehe <see cref="TestCaves"/>). Kein Original-Menüpunkt,
    /// sondern ein (auf dem Option-Screen unsichtbarer) Entwicklerzugang zum Prüfstand.</summary>
    public void MenuTestMode()
    {
        if (Phase != SessionPhase.Menu) return;
        ResetIdleTimer();
        Phase = SessionPhase.TestMenu;
    }

    public void TestMenuNext()
    {
        if (Phase != SessionPhase.TestMenu) return;
        TestCaveIndex = Math.Min(TestCaveIndex + 1, TestCaves.Count - 1);
    }

    public void TestMenuPrevious()
    {
        if (Phase != SessionPhase.TestMenu) return;
        TestCaveIndex = Math.Max(TestCaveIndex - 1, 0);
    }

    /// <summary>Direktwahl über die Zifferntasten 1-5.</summary>
    public void TestMenuSelect(int index)
    {
        if (Phase != SessionPhase.TestMenu || index < 0 || index >= TestCaves.Count) return;
        TestCaveIndex = index;
    }

    public void TestMenuBack()
    {
        if (Phase != SessionPhase.TestMenu) return;
        Phase = SessionPhase.Menu;
    }

    /// <summary>Startet die gewählte Prüfstand-Cave. Sie steht außerhalb der PlayOrder — jedes
    /// Cave-Ende führt daher zurück in den Testmodus (bzw. lädt sie bei Tod/Zeitablauf erneut).</summary>
    public void TestMenuStart()
    {
        if (Phase != SessionPhase.TestMenu) return;

        State.Chances = 3;
        _isTestCave = TryLoadCave(_caves.Get(TestCaves[TestCaveIndex].Name));
        if (!_isTestCave)
        {
            Phase = SessionPhase.TestMenu; // Cave ohne Eingang — Sicherheitsnetz wie in LoadCaveWithSkip.
        }
    }

    /// <summary>Demo starten (Attract-Mode nach Leerlauf) — lädt wie im BD1-Original IMMER
    /// Cave A auf Level 1, unabhängig von der Menü-Auswahl (die lebt in _menuCaveSlot weiter
    /// und bleibt unberührt). Chances/leben bleiben unangetastet. Startet direkt in
    /// DemoPlaying: die BD1-Aufzeichnung enthält ihre Anlauf-Wartezeit selbst (demo.txt
    /// beginnt mit "Wait 15"-Zügen) — das delay(7000) des DOS-Originals (BOULDER.CPP:359)
    /// war nur eine Krücke seines Scancode-Formats und entfällt.</summary>
    public void StartDemo()
    {
        if (Phase is not (SessionPhase.TitleScreen or SessionPhase.Menu) || _demoSteps is null)
        {
            return;
        }

        _isDemo = true;
        _isTestCave = false;
        LoadCaveWithSkip(0, 1);

        if (Phase != SessionPhase.Playing)
        {
            // Cave A war nicht ladbar (Sicherheitsnetz in LoadCaveWithSkip) — kein Demo-Start.
            _isDemo = false;
            return;
        }

        _demoPlayer = new DemoPlayer(_demoSteps);
        Phase = SessionPhase.DemoPlaying;
        _demoPlayer.ApplyCurrent(Input, Cave!.Width);
    }

    /// <summary>Beliebige Taste während der Demo: bricht sie ab und führt (über das übliche
    /// Zudecken) zum Titelbildschirm zurück — wie im BD1-Attract-Zyklus.</summary>
    public void DemoInterrupted()
    {
        if (Phase != SessionPhase.DemoPlaying) return;
        BeginTransition(TransitionReason.DemoEnd);
    }

    /// <summary>Escape während des Spiels: beendet die Session, kehrt (nach der üblichen
    /// Übergangspause) ins Menü zurück — BOULDER.CPP:391 (c==0x01 beendet game_start()).</summary>
    public void EscapePressed()
    {
        if (Phase != SessionPhase.Playing) return;
        BeginTransition(TransitionReason.EscQuit);
    }

    /// <summary>Für DeathPause/GameOverMessage: entspricht dem abschließenden getch().</summary>
    public void AnyKeyPressed()
    {
        if (Phase == SessionPhase.DeathPause && _phaseTimer <= 0)
        {
            BeginTransition(State.Chances < 1 ? TransitionReason.GameOver : TransitionReason.DeathRetry);
        }
        else if (Phase == SessionPhase.GameOverMessage && _phaseTimer <= 0)
        {
            BeginTransition(TransitionReason.GameOver);
        }
    }

    /// <summary>Treibt Zähler, Kamera-Scroll und (solange IsCaveEnded=false) Physik einen oder
    /// mehrere Ticks weiter. Im Original bleibt die Timer-ISR bis Init_ISR(DEINSTALL) aktiv —
    /// das passiert in game_start() ERST NACH den Todes-/Bonus-Pausen (BOULDER.CPP:405-422),
    /// weshalb Animation (und bei einem Tod ohne level_ende auch die Physik) während
    /// LevelEndBonus/DeathPause/GameOverMessage weiterläuft, nicht erst wieder in Playing.
    /// Nur während CaveTransition ist die ISR im Original bereits deinstalliert (Start_menu:
    /// delay(500);level_out(); läuft nach game_start()s Rückkehr) — dort bewusst kein Tick.</summary>
    private void AdvanceSimulation(double deltaSeconds)
    {
        if (Cave is null)
        {
            return;
        }

        _tickAccumulator += deltaSeconds;
        const int maxTicksPerFrame = 8;
        var ticks = 0;
        while (_tickAccumulator >= _secondsPerTick && ticks < maxTicksPerFrame)
        {
            _gameTick.Tick(Cave, Clocks, _entranceIndex);
            _tickAccumulator -= _secondsPerTick;
            ticks++;
        }
    }

    private void UpdatePlaying(double deltaSeconds)
    {
        if (Cave is null || CurrentCaveData is null)
        {
            return;
        }

        AdvanceSimulation(deltaSeconds);

        // Original prüft in game_start() ZWEI unabhängige Abbruchbedingungen
        // (BOULDER.CPP:397-398: "if(level_ende==0xFF) break; if(stat!=0) break;") — ein Tod
        // durch einen fallenden Stein setzt level_ende NICHT, nur stat.
        if (State.Stat != 0)
        {
            // Level_End() zählt Bonus nur bei stat==0 — bei Tod also nie, unabhängig von IsCaveEnded.
            State.Chances--;
            Phase = SessionPhase.DeathPause;
            _phaseTimer = DeathPauseSeconds;
            ShowGameOverMessage = false;
        }
        else if (State.IsCaveEnded)
        {
            BeginLevelEndBonus();
        }
    }

    /// <summary>Eigene Tick-Schleife statt AdvanceSimulation: der Demo-Vorschub muss nach JEDEM
    /// einzelnen GameTick.Tick()-Aufruf laufen (siehe DemoPlayer-Klassenkommentar), nicht erst
    /// nach der ganzen Frame-Charge.</summary>
    private void UpdateDemoPlaying(double deltaSeconds)
    {
        if (Cave is null || CurrentCaveData is null || _demoPlayer is null)
        {
            return;
        }

        _tickAccumulator += deltaSeconds;
        const int maxTicksPerFrame = 8;
        var ticks = 0;
        while (_tickAccumulator >= _secondsPerTick && ticks < maxTicksPerFrame)
        {
            _gameTick.Tick(Cave, Clocks, _entranceIndex);
            _demoPlayer.AdvanceIfDue(Clocks, Input, Cave.Width);
            _tickAccumulator -= _secondsPerTick;
            ticks++;

            if (State.Stat != 0 || State.IsCaveEnded || _demoPlayer.IsAtEnd)
            {
                break;
            }
        }

        EvaluateDemoEnd();
    }

    /// <summary>Abbruchprioritäten wie in demo() pro Foreground-Iteration geprüft (BOULDER.CPP:
    /// 369-374): zuerst Cave-Ende (level_ende, zählt bei stat==0 noch Bonus wie im Normalspiel),
    /// dann Tod (kein Bonus, keine Chances/DeathPause — das Original fasst leben in demo() nicht
    /// an), erst danach der Demo-Terminator.</summary>
    private void EvaluateDemoEnd()
    {
        if (Cave is null || _demoPlayer is null)
        {
            return;
        }

        if (State.IsCaveEnded)
        {
            BeginLevelEndBonus();
        }
        else if (State.Stat != 0 || _demoPlayer.IsAtEnd)
        {
            BeginTransition(TransitionReason.DemoEnd);
        }
    }

    private double _bonusSubTimer;
    private bool _postBonusPauseActive;

    /// <summary>BD1-Quirk: Zieht Rockford genau in dem Tick in den Ausgang, in dem die Zeit auf 0
    /// fällt (GameTick lässt ihn dort noch ziehen), startet die Bonuszählung bei 0 — und der Zähler
    /// läuft als Byte unter. Der Spieler sieht die Zeit von 255 herunterlaufen und kassiert dafür
    /// 255 Gratispunkte. Beim bloßen Zeitablauf ohne Ausgang (AdvanceToNextCave==false) passiert das
    /// nicht: dort bleibt es bei 0 Bonuspunkten. Das DOS-Original kannte den Quirk nicht — dort
    /// gewinnt der Zeitablauf gegen den Ausgang (BOULDER.CPP:251-255) und Level_End() zählt mit
    /// vorheriger Prüfung (GAME.CPP:54).</summary>
    private void BeginLevelEndBonus()
    {
        Phase = SessionPhase.LevelEndBonus;
        _bonusSubTimer = 0;
        _postBonusPauseActive = false;

        if (State.AdvanceToNextCave && State.CaveTimeRemaining == 0)
        {
            State.CaveTimeRemaining = byte.MaxValue;
        }
    }

    private void UpdateLevelEndBonus(double deltaSeconds)
    {
        AdvanceSimulation(deltaSeconds);

        if (State.CaveTimeRemaining > 0)
        {
            _bonusSubTimer += deltaSeconds;
            while (_bonusSubTimer >= BonusSecondsPerPoint && State.CaveTimeRemaining > 0)
            {
                _bonusSubTimer -= BonusSecondsPerPoint;
                State.CaveTimeRemaining--;
                State.Score++;
                State.SoundEvents.Enqueue(SoundEvent.BonusCount);

                // "Note that for the last 10 seconds, the running out of time sound is still played,
                // at higher speed than normal" (Peter Broadribb, Bonus points sound): dieselbe
                // Zeitwarnung wie im Spiel (GameTick), hier aber im 20-ms-Takt der Bonuszählung statt
                // im Sekundentakt — der Zähler läuft ja durch dieselben Restsekunden 9..0.
                if (State.CaveTimeRemaining <= 9)
                {
                    State.SoundEvents.Enqueue(SoundEvent.TimeWarning);
                }
            }

            if (State.CaveTimeRemaining > 0)
            {
                return;
            }
        }

        // Zählschleife fertig (oder gar nicht nötig, Original läuft delay(1000) so oder so).
        if (!_postBonusPauseActive)
        {
            _postBonusPauseActive = true;
            _phaseTimer = PostBonusPauseSeconds;
        }

        _phaseTimer -= deltaSeconds;
        if (_phaseTimer <= 0)
        {
            _postBonusPauseActive = false;
            var reason = _isDemo
                ? TransitionReason.DemoEnd
                : State.AdvanceToNextCave ? TransitionReason.Success : TransitionReason.TimeoutAlive;
            BeginTransition(reason);
        }
    }

    private void UpdateDeathPause(double deltaSeconds)
    {
        AdvanceSimulation(deltaSeconds);

        _phaseTimer -= deltaSeconds;
        if (_phaseTimer > 0)
        {
            return;
        }

        if (State.Chances < 1 && !ShowGameOverMessage)
        {
            ShowGameOverMessage = true;
            Phase = SessionPhase.GameOverMessage;
            _phaseTimer = GameOverExtraPauseSeconds;
        }

        // Bei verbleibenden Leben wartet AnyKeyPressed() ab hier auf eine Taste (_phaseTimer<=0).
    }

    private void UpdateGameOverMessage(double deltaSeconds)
    {
        AdvanceSimulation(deltaSeconds);

        _phaseTimer -= deltaSeconds;
        // AnyKeyPressed() übernimmt ab _phaseTimer<=0.
    }

    /// <summary>Zudeck-Animation am Cave-Ende (BD1): die Stahlwand schiebt sich Runde für Runde
    /// wieder über die Cave. Die Physik ruht dabei — genau wie beim Aufdecken (GameTick); Ticks
    /// laufen weiter, treiben aber nur noch Zähler und Animation. Danach erst die Übergangspause
    /// (delay(500)).</summary>
    private void UpdateScreenCovering(double deltaSeconds)
    {
        AdvanceSimulation(deltaSeconds);

        if (_cover.IsActive)
        {
            return;
        }

        Phase = SessionPhase.CaveTransition;
        _phaseTimer = CaveTransitionPauseSeconds;
    }

    private void UpdateCaveTransition(double deltaSeconds)
    {
        _phaseTimer -= deltaSeconds;
        if (_phaseTimer > 0)
        {
            return;
        }

        // Die Prüfstand-Caves stehen außerhalb der PlayOrder: Tod und Zeitablauf laden dieselbe Cave
        // erneut, jedes andere Ende führt zurück in den Testmodus (statt in die nächste Cave der Kette).
        if (_isTestCave)
        {
            if (_transitionReason is TransitionReason.TimeoutAlive or TransitionReason.DeathRetry)
            {
                TryLoadCave(_caves.Get(TestCaves[TestCaveIndex].Name));
            }
            else
            {
                _isTestCave = false;
                ReturnToMenu(SessionPhase.TestMenu);
            }

            return;
        }

        switch (_transitionReason)
        {
            case TransitionReason.Success:
                var next = CaveIndex + 1;
                CaveIndex = (sbyte)(next >= PlayOrder.Length ? 0 : next);
                LoadCaveWithSkip(CaveIndex);
                break;
            case TransitionReason.TimeoutAlive:
            case TransitionReason.DeathRetry:
                LoadCaveWithSkip(CaveIndex);
                break;
            case TransitionReason.GameOver:
            case TransitionReason.EscQuit:
                ReturnToMenu();
                break;
            case TransitionReason.DemoEnd:
                // Wie im BD1-Attract-Zyklus zurück zum Titelbildschirm; die Menü-Auswahl in
                // _menuCaveSlot war vom Demo-Lauf nie betroffen.
                ReturnToMenu(SessionPhase.TitleScreen);
                _isDemo = false;
                _demoPlayer = null;
                break;
        }
    }

    /// <summary>Einziger Choke-Point aller Cave-Enden (Erfolg, Zeitablauf, Tod, Escape, Demo-Ende):
    /// erst deckt sich der Bildschirm zu (BD1), dann läuft die Übergangspause.</summary>
    private void BeginTransition(TransitionReason reason)
    {
        _transitionReason = reason;
        _cover.BeginCover();
        Phase = SessionPhase.ScreenCovering;
    }

    /// <summary>Lädt eine Cave über PlayOrder/DifficultyLevel (oder dem optionalen Level-Override,
    /// für die Demo, die immer Level 1 spielt); überspringt (mit Sicherheitsgrenze) Caves ohne
    /// Eingang, falls ein Repository doch einmal eine unspielbare Cave liefert.</summary>
    private void LoadCaveWithSkip(int caveIndex, int? levelOverride = null)
    {
        var level = levelOverride ?? DifficultyLevel;
        for (var attempt = 0; attempt < PlayOrder.Length; attempt++)
        {
            var index = (caveIndex + attempt) % PlayOrder.Length;
            if (!TryLoadCave(_caves.Get(NameFor(index, level))))
            {
                continue;
            }

            CaveIndex = (sbyte)index;
            return;
        }

        // Sicherheitsnetz: keine spielbare Cave gefunden -> zurück ins Menü statt hängen zu bleiben.
        Phase = SessionPhase.Menu;
    }

    /// <summary>Baut das Simulationsgitter auf und versetzt die Session ins Spiel. Scheitert (false),
    /// wenn die Cave keinen Eingang hat und damit unspielbar wäre.</summary>
    private bool TryLoadCave(CaveData data)
    {
        var cave = new Simulation.Cave(data, State, Input, Camera, _random);
        var entranceIndex = cave.FindFirstIndexOf(Element.Entrance);
        if (entranceIndex < 0)
        {
            return false;
        }

        CurrentCaveData = data;
        Cave = cave;
        _entranceIndex = entranceIndex;
        State.ResetForCave(data);
        Input.ResetForNewCave();
        Camera.CenterOn(entranceIndex % cave.Width, entranceIndex / cave.Width, cave.Width, cave.Height);
        _cover.BeginUncover(cave.Width, cave.Height);

        // Tempo der geladenen Cave (BD1: ergibt sich aus Schwierigkeitsgrad und Cave-Art, siehe
        // CaveSpeed). Die Clk18-Periode wird mitgesetzt, damit die Spielsekunde tempo-unabhängig
        // gleich lang bleibt.
        _secondsPerTick = data.GameSpeed.SecondsPerTick;
        Clocks.Reset(data.GameSpeed.GameSecondTicks);
        _tickAccumulator = 0;
        _bonusSubTimer = 0;

        Phase = SessionPhase.Playing;
        return true;
    }

    /// <summary>Spielflächen-Zoom: stellt die Sichtfenstergröße ein (siehe ViewportSize). Läuft
    /// gerade eine Cave, wird die Kamera neu geklemmt; steht Rockford danach außerhalb des
    /// Sichtfensters, holen ihn die Scroll-Auslöser im nächsten Cave-Scan kachelweise ein. Der
    /// Simulationszustand bleibt unberührt — das Sichtfenster ist reine Darstellung.</summary>
    public void SetViewport(ViewportSize viewport)
    {
        Camera.Viewport = viewport;
        if (Cave is not null)
        {
            Camera.Clamp(Cave.Width, Cave.Height);
        }
    }

    /// <summary>Verlässt die laufende Cave. <paramref name="phase"/> ist für die Prüfstand-Caves
    /// SessionPhase.TestMenu (damit man dort direkt die nächste auswählen kann) und für das
    /// Demo-Ende SessionPhase.TitleScreen (BD1-Attract-Zyklus).</summary>
    private void ReturnToMenu(SessionPhase phase = SessionPhase.Menu)
    {
        Phase = phase;
        Cave = null;
        CurrentCaveData = null;
        ShowGameOverMessage = false;
        ResetIdleTimer();
    }
}
