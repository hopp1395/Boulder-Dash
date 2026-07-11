using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Flow;

public enum SessionPhase
{
    Menu,
    Playing,
    LevelEndBonus,
    DeathPause,
    GameOverMessage,
    CaveTransition,
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
/// - Die Menü-Cave-Auswahl ist auf 0..15 (A-P) begrenzt (Original: cavenr&gt;15→15), obwohl
///   LEVEL.BIN 21 Datensätze enthält — passend zum Handbuch ("16 CAVES... A through P").
///   Caves 16..20 sind nur über erfolgreiche Fortschritts-Kette erreichbar; Cave 20 ist ein
///   leerer, nie fertiggestellter Platzhalter (siehe CaveFileTests) — wird beim Laden übersprungen,
///   um die im Original mögliche Endlosschleife/undefinierte Speicherlesung zu vermeiden.
/// </summary>
public sealed class GameSession
{
    private const int MaxCaveIndex = 15; // Menü-Auswahl A..P (Original: cavenr>15 -> 15)
    private const double BonusSecondsPerPoint = 0.02; // delay(20) pro Punkt, GAME.CPP:58
    private const double PostBonusPauseSeconds = 1.0; // delay(1000) nach der Bonuszählung
    private const double DeathPauseSeconds = 1.0; // delay(1000)
    private const double GameOverExtraPauseSeconds = 1.0; // zweites delay(1000) vor GAME OVER
    private const double CaveTransitionPauseSeconds = 0.5; // delay(500) vor level_out()
    private const double MarqueeSecondsPerChar = 3.0 / 18.2; // clk_18-Periode 3 bei 18,2 Hz Standardtimer

    // Original-Marquee-Text (BOULDER.CPP:206-210), Umlaut ü->ue ersetzt (siehe BiosFont).
    // "Boulde Dash" (ohne r) ist ein Tippfehler im Original und wird bewusst nicht korrigiert.
    private const string MarqueeText =
        "                                        Boulde Dash v1.1                        " +
        "Copyright by Jan Hoppe 1999             " +
        "Druecken Sie bitte die F1 Taste um das Sp" +
        "iel zu starten                          " +
        "                                        ";

    private readonly IReadOnlyList<CaveData> _caves;
    private readonly BorlandRandom _random;
    private readonly CavePhysics _physics;
    private readonly Dissolve _dissolve;
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

    public SessionPhase Phase { get; private set; } = SessionPhase.Menu;
    public sbyte CaveIndex { get; private set; }
    public sbyte DifficultyLevel { get; private set; } = 1;
    public bool QuitRequested { get; private set; }
    public bool ShowGameOverMessage { get; private set; }

    public GameState State { get; }
    public InputState Input { get; }
    public Camera Camera { get; }
    public Clocks Clocks { get; }
    public Cave? Cave { get; private set; }
    public CaveData? CurrentCaveData { get; private set; }

    /// <summary>Auflöse-Overlay für die Rendering-Schicht (newmask-Äquivalent) — eine Instanz für
    /// die gesamte Session, da level_in() und regel() im Original denselben rand()-Strom teilen.</summary>
    public Dissolve Dissolve => _dissolve;

    public GameSession(IReadOnlyList<CaveData> caves)
    {
        _caves = caves;
        _random = new BorlandRandom();
        _physics = new CavePhysics(_random);
        _dissolve = new Dissolve(_random);
        _gameTick = new GameTick(_physics, _dissolve);

        State = new GameState();
        Input = new InputState();
        Camera = new Camera();
        Clocks = new Clocks();
    }

    public string MarqueeVisibleText => MarqueeText.Substring(_marqueeOffset, 40);

    /// <summary>Cave-Buchstabe der aktuell im Menü gewählten (nicht zwingend geladenen) Cave.</summary>
    public char SelectedCaveLetter => LetterFor(CaveIndex);

    private static char LetterFor(int caveIndex)
    {
        var korrektur = (caveIndex - 1) / 4;
        return (char)(caveIndex + 'A' - korrektur);
    }

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
            case SessionPhase.CaveTransition:
                UpdateCaveTransition(deltaSeconds);
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
        if (DifficultyLevel++ > 3) DifficultyLevel = 4;
    }

    public void MenuDown()
    {
        if (Phase != SessionPhase.Menu) return;
        if (DifficultyLevel-- < 2) DifficultyLevel = 1;
    }

    public void MenuNextCave()
    {
        if (Phase != SessionPhase.Menu) return;
        CaveIndex++;
        if (CaveIndex > MaxCaveIndex) CaveIndex = MaxCaveIndex;
    }

    public void MenuPreviousCave()
    {
        if (Phase != SessionPhase.Menu) return;
        CaveIndex--;
        if (CaveIndex < 0) CaveIndex = 0;
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
        LoadCaveWithSkip(CaveIndex);
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

    private void UpdatePlaying(double deltaSeconds)
    {
        if (Cave is null || CurrentCaveData is null)
        {
            return;
        }

        _tickAccumulator += deltaSeconds;
        const int maxTicksPerFrame = 8;
        var ticks = 0;
        while (_tickAccumulator >= _secondsPerTick && ticks < maxTicksPerFrame && !State.IsCaveEnded)
        {
            _gameTick.Tick(Cave, State, Input, Camera, Clocks, _entranceIndex);
            _tickAccumulator -= _secondsPerTick;
            ticks++;
        }

        if (State.IsCaveEnded)
        {
            if (State.Stat == 0)
            {
                Phase = SessionPhase.LevelEndBonus;
                _bonusSubTimer = 0;
                _postBonusPauseActive = false;
            }
            else
            {
                State.Chances--;
                Phase = SessionPhase.DeathPause;
                _phaseTimer = DeathPauseSeconds;
                ShowGameOverMessage = false;
            }
        }
    }

    private double _bonusSubTimer;
    private bool _postBonusPauseActive;

    private void UpdateLevelEndBonus(double deltaSeconds)
    {
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
            BeginTransition(State.AdvanceToNextCave ? TransitionReason.Success : TransitionReason.TimeoutAlive);
        }
    }

    private void UpdateDeathPause(double deltaSeconds)
    {
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
        _phaseTimer -= deltaSeconds;
        // AnyKeyPressed() übernimmt ab _phaseTimer<=0.
    }

    private void UpdateCaveTransition(double deltaSeconds)
    {
        _phaseTimer -= deltaSeconds;
        if (_phaseTimer > 0)
        {
            return;
        }

        switch (_transitionReason)
        {
            case TransitionReason.Success:
                var next = CaveIndex + 1;
                CaveIndex = (sbyte)(next >= _caves.Count ? 0 : next);
                LoadCaveWithSkip(CaveIndex);
                break;
            case TransitionReason.TimeoutAlive:
            case TransitionReason.DeathRetry:
                LoadCaveWithSkip(CaveIndex);
                break;
            case TransitionReason.GameOver:
            case TransitionReason.EscQuit:
                Phase = SessionPhase.Menu;
                Cave = null;
                CurrentCaveData = null;
                ShowGameOverMessage = false;
                break;
        }
    }

    private void BeginTransition(TransitionReason reason)
    {
        _transitionReason = reason;
        Phase = SessionPhase.CaveTransition;
        _phaseTimer = CaveTransitionPauseSeconds;
    }

    /// <summary>Lädt eine Cave; überspringt (mit Sicherheitsgrenze) Caves ohne Eingang, wie den
    /// leeren Platzhalter Cave 20 — das Original würde dort in level_laden's Eingangssuche
    /// unbegrenzt weiterlesen (kein Byte==10 vorhanden), siehe CaveFileTests.</summary>
    private void LoadCaveWithSkip(int caveIndex)
    {
        for (var attempt = 0; attempt < _caves.Count; attempt++)
        {
            var index = (caveIndex + attempt) % _caves.Count;
            var data = _caves[index];
            var cave = new Cave(data);
            var entranceIndex = cave.FindFirstIndexOf(Element.Entrance);
            if (entranceIndex < 0)
            {
                continue;
            }

            CaveIndex = (sbyte)index;
            CurrentCaveData = data;
            Cave = cave;
            _entranceIndex = entranceIndex;
            State.ResetForCave(data);
            Camera.ResetTo(data.CameraStartX, data.CameraStartY);
            Clocks.Reset();

            var divisor = 59659 / data.GameSpeed;
            _secondsPerTick = divisor / 1193182.0;
            _tickAccumulator = 0;
            _bonusSubTimer = 0;

            Phase = SessionPhase.Playing;
            return;
        }

        // Sicherheitsnetz: keine spielbare Cave gefunden -> zurück ins Menü statt hängen zu bleiben.
        Phase = SessionPhase.Menu;
    }
}
