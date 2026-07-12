using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Flow;

public enum SessionPhase
{
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

    /// <summary>Wartephase nach F2, vor dem ersten Demo-Zug — entspricht delay(7000) in
    /// demo() (BOULDER.CPP:359), während dem der Eingang sich bereits aufbaut.</summary>
    DemoWait,
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

    /// <summary>Demo endet (Ende der Aufzeichnung, Cave-Ende oder Tod) — kehrt immer ins Menü
    /// zurück, wie der F2-Zweig in Start_menu (BOULDER.CPP:321-328).</summary>
    DemoEnd,
}

/// <summary>
/// Orchestriert Menü, Cave-Ablauf und Progression — Analog zu Start_menu()/game_start()/
/// Level_End() (src/BOULDER.CPP:273-423, GAME.CPP:34-62). Wandelt die im Original blockierenden
/// delay()/getch()-Aufrufe in einen Zustandsautomaten mit Restzeit um (siehe Update()).
///
/// Nicht-offensichtliche Original-Regeln, hier bewusst nachgebildet:
/// - Chances (leben) werden NUR bei F1-Druck auf 3 zurückgesetzt, nicht pro Cave.
/// - Zeitablauf OHNE Tod (Rockford lebt, Diamanten/Ausgang nicht erreicht) kostet kein Leben
///   und lädt dieselbe Cave erneut (level_next bleibt 0).
/// - Nur ein Ausgangs-Erfolg (AdvanceToNextCave) rückt zur nächsten Cave vor.
/// - Escape während des Spiels beendet die Session und kehrt ins Menü zurück (nicht nur die Cave),
///   durchläuft aber noch die Übergangs-Pause wie jedes andere Cave-Ende.
/// - Die Menü-Cave-Auswahl ist auf die 16 reg. Caves A-P begrenzt (passend zum Handbuch
///   "16 CAVES... A through P"); die 4 Intermissions (Q-T) sind nur über die Fortschritts-Kette
///   nach jeder 4. Cave erreichbar (Original-BD1-Struktur), siehe PlayOrder.
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
    /// erscheint. Der Titel darf höchstens 35 Zeichen haben — der MenuRenderer stellt Auswahlmarke und
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
        new("cave-test-4", "SCHIEBEN GELINGT NUR MIT 1 ZU 8"),
        new("cave-test-5", "ZAUBERMAUER WANDELT UM UND KLINGT"),
    ];

    private const int MaxCaveIndex = 18; // Menü-Auswahl A..P (letzte reguläre Cave in PlayOrder, Index 18=P)
    private const double BonusSecondsPerPoint = 0.02; // delay(20) pro Punkt, GAME.CPP:58
    private const double PostBonusPauseSeconds = 1.0; // delay(1000) nach der Bonuszählung
    private const double DeathPauseSeconds = 1.0; // delay(1000)
    private const double GameOverExtraPauseSeconds = 1.0; // zweites delay(1000) vor GAME OVER
    private const double CaveTransitionPauseSeconds = 0.5; // delay(500) vor level_out()
    private const double MarqueeSecondsPerChar = 3.0 / 18.2; // clk_18-Periode 3 bei 18,2 Hz Standardtimer
    private const double DemoWaitSeconds = 7.0; // delay(7000), BOULDER.CPP:359

    // Original-Marquee-Text (BOULDER.CPP:206-210), Umlaut ü->ue ersetzt (siehe BiosFont).
    // "Boulde Dash" (ohne r) ist ein Tippfehler im Original und wird bewusst nicht korrigiert.
    private const string MarqueeText =
        "                                        Boulde Dash v1.1                        " +
        "Copyright by Jan Hoppe 1999             " +
        "Druecken Sie bitte die F1 Taste um das Sp" +
        "iel zu starten                          " +
        "                                        ";

    private readonly ICaveRepository _caves;
    private readonly Random _random;
    private readonly CavePhysics _physics;
    private readonly ScreenCover _cover;
    private readonly GameTick _gameTick;

    private double _phaseTimer;
    private double _tickAccumulator;
    private double _secondsPerTick;
    private double _marqueeTimer;
    private int _marqueeOffset;

    /// <summary>Wie das Original-`start` (WORD, BOULDER.CPP:112): einmal beim Laden ermittelt und
    /// danach unverändert weiterverwendet — die Eingangskachel wird während des Aufbaus zu
    /// Explosion und dann zu Rockford, ein erneutes Suchen würde ab da fehlschlagen.</summary>
    private int _entranceIndex;
    private TransitionReason _transitionReason;

    private readonly IReadOnlyList<DemoStep>? _demoSteps;
    private DemoPlayer? _demoPlayer;
    private bool _isDemo;
    private bool _isTestCave;
    private sbyte _menuCaveIndexBeforeDemo;

    public SessionPhase Phase { get; private set; } = SessionPhase.Menu;
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
        _physics = new CavePhysics(_random);
        _cover = new ScreenCover(_random);
        _gameTick = new GameTick(_physics, _cover);

        State = new GameState();
        Input = new InputState();
        Camera = new Camera();
        Clocks = new Clocks();
    }

    public string MarqueeVisibleText => MarqueeText.Substring(_marqueeOffset, 40);

    /// <summary>Cave-Buchstabe der aktuell im Menü gewählten (nicht zwingend geladenen) Cave.</summary>
    public char SelectedCaveLetter => PlayOrder[CaveIndex];

    /// <summary>Name, unter dem eine Cave im Repository liegt (= Dateiname ohne Endung).</summary>
    private static string NameFor(int playIndex, int level) => $"cave-{PlayOrder[playIndex]}-{level}";

    /// <summary>Ob die Cave an dieser Stelle der Spielreihenfolge eine Intermission ist — steht in
    /// der Cave-Datei selbst (Kind=Intermission) und ist für alle 5 Level gleich.</summary>
    private bool IsIntermission(int playIndex) => _caves.Get(NameFor(playIndex, 1)).IsIntermission;

    public void Update(double deltaSeconds)
    {
        switch (Phase)
        {
            case SessionPhase.Menu:
                UpdateMarquee(deltaSeconds);
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
            case SessionPhase.DemoWait:
                UpdateDemoWait(deltaSeconds);
                break;
            case SessionPhase.DemoPlaying:
                UpdateDemoPlaying(deltaSeconds);
                break;
        }
    }

    private void UpdateMarquee(double deltaSeconds)
    {
        _marqueeTimer += deltaSeconds;
        while (_marqueeTimer >= MarqueeSecondsPerChar)
        {
            _marqueeTimer -= MarqueeSecondsPerChar;
            _marqueeOffset++;
            // Original-Bug (uninitialisierte Vergleichsvariable) verhindert hier ein sauberes
            // Zurückspringen — wir implementieren das offensichtlich beabsichtigte Verhalten.
            if (_marqueeOffset > MarqueeText.Length - 40)
            {
                _marqueeOffset = 0;
            }
        }
    }

    // Menü-Eingaben: entspricht dem switch in Start_menu (BOULDER.CPP:291-329). Die Tasten
    // wirken hier nach ihrer SINNVOLLEN Bedeutung (rechts=vor, links=zurück) — im Original
    // sind die #define-Namen RECHTS/LINKS vertauscht, die tatsächlich abgefragten Scancodes
    // (0x4B/0x4D) ergeben aber exakt dieses Verhalten, siehe InputState-Klassenkommentar.
    public void MenuUp()
    {
        if (Phase != SessionPhase.Menu) return;
        if (DifficultyLevel++ > 4) DifficultyLevel = 5;
    }

    public void MenuDown()
    {
        if (Phase != SessionPhase.Menu) return;
        if (DifficultyLevel-- < 2) DifficultyLevel = 1;
    }

    public void MenuNextCave()
    {
        if (Phase != SessionPhase.Menu) return;
        var next = CaveIndex + 1;
        while (next <= MaxCaveIndex && IsIntermission(next)) next++;
        CaveIndex = next > MaxCaveIndex ? (sbyte)MaxCaveIndex : (sbyte)next;
    }

    public void MenuPreviousCave()
    {
        if (Phase != SessionPhase.Menu) return;
        var previous = CaveIndex - 1;
        while (previous >= 0 && IsIntermission(previous)) previous--;
        CaveIndex = previous < 0 ? (sbyte)0 : (sbyte)previous;
    }

    public void MenuQuit()
    {
        if (Phase != SessionPhase.Menu) return;
        QuitRequested = true;
    }

    /// <summary>F1: neue Session starten (leben=3, aktuelle Menü-Cave laden).</summary>
    public void MenuStart()
    {
        if (Phase != SessionPhase.Menu) return;
        State.Chances = 3;
        _isTestCave = false;
        LoadCaveWithSkip(CaveIndex);
    }

    /// <summary>F5: in den Testmodus wechseln (siehe <see cref="TestCaves"/>). Kein Original-Menüpunkt,
    /// sondern ein Entwicklerzugang zum Prüfstand für das Objektverhalten.</summary>
    public void MenuTestMode()
    {
        if (Phase != SessionPhase.Menu) return;
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

    /// <summary>F2: Demo starten — lädt IMMER Cave A (level_laden(0) in Start_menu, BOULDER.CPP:322),
    /// unabhängig von der Menü-Cave-Auswahl. Chances/leben bleiben unangetastet (das Original
    /// fasst leben nur in F1 an). level_laden(0) schreibt (anders als die F1-Progression) NICHT
    /// in cavenr — die Menü-Auswahl muss daher separat gesichert und danach wiederhergestellt
    /// werden (LoadCaveWithSkip setzt CaveIndex sonst dauerhaft auf 0).</summary>
    public void MenuDemo()
    {
        if (Phase != SessionPhase.Menu || _demoSteps is null)
        {
            return;
        }

        _isDemo = true;
        _isTestCave = false;
        _menuCaveIndexBeforeDemo = CaveIndex;
        LoadCaveWithSkip(0, 1);

        if (Phase != SessionPhase.Playing)
        {
            // Cave A war nicht ladbar (Sicherheitsnetz in LoadCaveWithSkip) — kein Demo-Start.
            _isDemo = false;
            return;
        }

        _demoPlayer = new DemoPlayer(_demoSteps);
        Phase = SessionPhase.DemoWait;
        _phaseTimer = DemoWaitSeconds;
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
            _gameTick.Tick(Cave, State, Input, Camera, Clocks, _entranceIndex);
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
            Phase = SessionPhase.LevelEndBonus;
            _bonusSubTimer = 0;
            _postBonusPauseActive = false;
        }
    }

    private void UpdateDemoWait(double deltaSeconds)
    {
        AdvanceSimulation(deltaSeconds); // ISR läuft weiter, Eingang baut sich auf (wie im Original während delay(7000)).

        _phaseTimer -= deltaSeconds;
        if (_phaseTimer > 0)
        {
            return;
        }

        Phase = SessionPhase.DemoPlaying;
        _demoPlayer!.ApplyCurrent(Input, Cave!.Width);
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
            _gameTick.Tick(Cave, State, Input, Camera, Clocks, _entranceIndex);
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
            Phase = SessionPhase.LevelEndBonus;
            _bonusSubTimer = 0;
            _postBonusPauseActive = false;
        }
        else if (State.Stat != 0 || _demoPlayer.IsAtEnd)
        {
            BeginTransition(TransitionReason.DemoEnd);
        }
    }

    private double _bonusSubTimer;
    private bool _postBonusPauseActive;

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

    /// <summary>Zudeck-Animation am Cave-Ende (BD1): die Simulation läuft dabei weiter (wie im
    /// Original die ISR bis zum Ende von game_start()), nur die Stahlwand schiebt sich Runde für
    /// Runde wieder über die Cave. Danach erst die Übergangspause (delay(500)).</summary>
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
                ReturnToMenu();
                _isDemo = false;
                _demoPlayer = null;
                CaveIndex = _menuCaveIndexBeforeDemo;
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
        var cave = new Simulation.Cave(data);
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
        Camera.ResetTo(data.CameraStartX, data.CameraStartY);
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

    /// <summary>Verlässt die laufende Cave. <paramref name="phase"/> ist für die Prüfstand-Caves
    /// SessionPhase.TestMenu, damit man dort direkt die nächste auswählen kann.</summary>
    private void ReturnToMenu(SessionPhase phase = SessionPhase.Menu)
    {
        Phase = phase;
        Cave = null;
        CurrentCaveData = null;
        ShowGameOverMessage = false;
    }
}
