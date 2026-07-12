using BoulderDash.Core.Data;
using BoulderDash.Core.Flow;
using BoulderDash.Core.Simulation;
using BoulderDash.Game.Audio;
using BoulderDash.Game.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using XnaGame = Microsoft.Xna.Framework.Game;

namespace BoulderDash.Game;

/// <summary>
/// Haupteinstiegspunkt der MonoGame-Anwendung. Rendert intern auf ein RenderTarget in logischer
/// Auflösung (Menüs fest 320x200 wie der VGA-Mode 13h des Originals, im Spiel die Größe des
/// gewählten Sichtfensters) und skaliert es beim Zeichnen ganzzahlig und zentriert auf die
/// Fenstergröße hoch. Delegiert Menü-/Spielablauf komplett an GameSession (Core) und wählt hier
/// nur, welcher Renderer je nach SessionPhase zum Einsatz kommt.
///
/// Bildschirm-Zoom: das Fenster ist frei skalierbar, F11 schaltet randloses Vollbild.
/// Spielflächen-Zoom: + zoomt hinein, - hinaus. Eine Stufe ist der Kachelmaßstab, das Sichtfenster
/// ist, was bei diesem Maßstab auf die Zeichenfläche passt — die Stufen hängen deshalb an ihr und
/// werden bei jeder Größenänderung neu abgeleitet (siehe ViewportSteps, SyncViewportSteps).
/// Beides wird in der Einstellungsdatei gemerkt (siehe SettingsFile).
/// </summary>
public class BoulderDashGame : XnaGame
{
    /// <summary>Logische Auflösung der Menübildschirme (VGA-Mode 13h des Originals).</summary>
    private const int MenuWidth = 320;
    private const int MenuHeight = 200;

    private readonly GraphicsDeviceManager _graphics;
    private readonly string _settingsPath = SettingsFile.DefaultPath;
    private SpriteBatch _spriteBatch = null!;
    private RenderTarget2D _renderTarget = null!;

    private SpriteAtlas _spriteAtlas = null!;
    private CaveRenderer _caveRenderer = null!;
    private TitleRenderer _titleRenderer = null!;
    private TestMenuRenderer _testMenuRenderer = null!;
    private BiosFont _font = null!;
    private AudioPlayer _audioPlayer = null!;
    private InputAdapter _inputAdapter = null!;

    private GameSession _session = null!;

    /// <summary>Der gewünschte Spielflächen-Zoom (aus der Einstellungsdatei bzw. zuletzt mit +/-
    /// gewählt) — ein Wunsch, keine Tatsache: Gezeigt wird die Stufe der aktuellen Zeichenfläche, die
    /// ihm am nächsten kommt (SyncViewportSteps). Maßgeblich ist immer Camera.Viewport.</summary>
    private ViewportSize _viewport;

    private Point _windowedSize;
    private bool _applyingClientSize;

    /// <summary>Die Zoomstufen der aktuellen Zeichenfläche (siehe ViewportSteps) und die Fläche, aus
    /// der sie stammen — ändert sie sich (Fenster gezogen, Vollbild geschaltet), wird neu abgeleitet.</summary>
    private ViewportSteps _steps = null!;
    private Point _surface;

    /// <summary>Cave-Explore (E-Taste im Spiel, siehe ExploreMap). Die Session hält den maßgeblichen
    /// Schalter; hier steht er nur mit, damit SaveSettings ihn in die Einstellungsdatei schreiben kann.</summary>
    private bool _explore;

    private enum PaletteContext { None, Menu, Cave }

    private PaletteContext _paletteContext = PaletteContext.None;
    private CaveData? _lastPaletteCaveData;
    private Rgb? _lastExitOverride;
    private SessionPhase _lastPhase = SessionPhase.TitleScreen;

    public BoulderDashGame()
    {
        var settings = SettingsFile.Load(_settingsPath);
        _viewport = settings.Viewport;
        _explore = settings.Explore;
        _windowedSize = new Point(
            Math.Max(MenuWidth, settings.WindowWidth),
            Math.Max(MenuHeight, settings.WindowHeight));

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = _windowedSize.X,
            PreferredBackBufferHeight = _windowedSize.Y,
            // Randloses Vollbild (kein Moduswechsel der Grafikkarte) — das Bild wird ohnehin
            // ganzzahlig hochskaliert und mit schwarzem Rand zentriert.
            HardwareModeSwitch = false,
            IsFullScreen = settings.Fullscreen,
        };

        IsMouseVisible = false;
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += OnClientSizeChanged;
    }

    protected override void Initialize()
    {
        base.Initialize();
        EnsureRenderTarget(MenuWidth, MenuHeight);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
        var sprites = new SpriteTextRepository(Path.Combine(assets, "Sprites"));
        _spriteAtlas = new SpriteAtlas(GraphicsDevice, sprites);
        _caveRenderer = new CaveRenderer(_spriteAtlas);
        _font = new BiosFont(GraphicsDevice);
        _titleRenderer = new TitleRenderer(GraphicsDevice, Path.Combine(assets, "Screens"), _font);
        _testMenuRenderer = new TestMenuRenderer(_font);
        _audioPlayer = new AudioPlayer();
        _inputAdapter = new InputAdapter();

        var caves = new CaveTextRepository(Path.Combine(assets, "Caves"));
        var demoSteps = DemoTextFile.Load(Path.Combine(assets, "demo.txt"));
        _session = new GameSession(caves, demoSteps);
        _session.SetExplore(_explore);

        // Der gemerkte Zoom ist nur ein Wunschwert: Welche Stufen es gibt, weiß erst die Zeichenfläche.
        SyncViewportSteps();

        SyncPalette();
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        _inputAdapter.Update(keyboard);

        SyncViewportSteps();
        HandleShellInput();
        HandlePhaseInput();

        if (_session.Phase == SessionPhase.LevelEndBonus && _lastPhase != SessionPhase.LevelEndBonus)
        {
            _audioPlayer.ResetBonusSweep();
        }

        _lastPhase = _session.Phase;

        _session.Update(gameTime.ElapsedGameTime.TotalSeconds);

        SyncPalette();
        _audioPlayer.Update(_session.State);

        if (_session.Phase is SessionPhase.TitleScreen or SessionPhase.Menu or SessionPhase.TestMenu)
        {
            _audioPlayer.PlayMusic();
        }
        else
        {
            _audioPlayer.StopMusic();
        }

        if (_session.QuitRequested)
        {
            Exit();
        }

        base.Update(gameTime);
    }

    /// <summary>Bildschirm- und Spielflächen-Zoom. Läuft vor HandlePhaseInput und ist von der
    /// SessionPhase unabhängig — die Tasten gehören der Schale, nicht dem Spiel (siehe
    /// InputAdapter.ShellKeys).</summary>
    private void HandleShellInput()
    {
        if (_inputAdapter.IsJustPressed(Keys.F11))
        {
            ToggleFullscreen();
        }

        // Hineinzoomen (+) heißt näher heran, also WENIGER Kacheln im Bild — das kleinere Sichtfenster.
        var zoomIn = _inputAdapter.IsAnyJustPressed(Keys.OemPlus, Keys.Add);
        var zoomOut = _inputAdapter.IsAnyJustPressed(Keys.OemMinus, Keys.Subtract);
        if (zoomIn == zoomOut)
        {
            return;
        }

        var current = _session.Camera.Viewport;
        var next = zoomIn ? _steps.Smaller(current) : _steps.Larger(current);
        if (next == current)
        {
            return;
        }

        // Der Zoom ist gewollt: Er wird zum neuen Wunsch und damit gemerkt.
        _viewport = next;
        _session.SetViewport(next);
        SaveSettings();
    }

    /// <summary>
    /// Leitet die Zoomstufen aus der Zeichenfläche ab, sobald diese sich ändert — beim Start, nach dem
    /// Ziehen des Fensters und beim Umschalten ins Vollbild (siehe ViewportSteps) — und setzt das
    /// Sichtfenster auf die Stufe der neuen Leiter, die dem Wunsch (_viewport) am nächsten kommt.
    ///
    /// Gesnappt wird immer der WUNSCH, nicht die zuletzt gezeigte Größe: Sonst fräße ein kurzzeitig
    /// kleines Fenster den Zoom auf, weil er beim Zurückziehen nur noch von der kleinen Stufe aus
    /// zurückrechnen könnte. Zeichenflächen unterhalb des Menüschirms (320x200 — etwa ein minimiertes
    /// Fenster) werden deshalb ganz übergangen.
    ///
    /// Das Fenster wird beim Zoomen NICHT nachgezogen: Eine Stufe IST der Kachelmaßstab, und der soll
    /// sich beim Zoomen ja gerade ändern — jede Stufe füllt die Fläche dabei ganzzahlig aus.
    /// </summary>
    private void SyncViewportSteps()
    {
        var surface = new Point(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        if (surface == _surface || surface.X < MenuWidth || surface.Y < MenuHeight)
        {
            return;
        }

        _surface = surface;
        _steps = ViewportSteps.For(surface.X, surface.Y, CaveRenderer.TileSize, CaveRenderer.StatusLineHeight);

        var viewport = _steps.Snap(_viewport);
        if (viewport != _session.Camera.Viewport)
        {
            _session.SetViewport(viewport);
        }
    }

    private void ToggleFullscreen()
    {
        if (!_graphics.IsFullScreen)
        {
            // Fenstergröße merken, damit sie beim Zurückschalten wiederkommt.
            _windowedSize = new Point(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
        }
        else
        {
            _graphics.PreferredBackBufferWidth = _windowedSize.X;
            _graphics.PreferredBackBufferHeight = _windowedSize.Y;
        }

        _graphics.ToggleFullScreen();
        SaveSettings();
    }

    /// <summary>Zieht der Nutzer das Fenster, wird der Backbuffer nachgeführt; der Zeichenmaßstab
    /// ergibt sich daraus von selbst (GetIntegerScale). Der Guard verhindert die Rückkopplung,
    /// weil ApplyChanges seinerseits ClientSizeChanged auslöst.</summary>
    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        if (_applyingClientSize || _graphics.IsFullScreen)
        {
            return;
        }

        var bounds = Window.ClientBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        _applyingClientSize = true;
        try
        {
            _graphics.PreferredBackBufferWidth = bounds.Width;
            _graphics.PreferredBackBufferHeight = bounds.Height;
            _graphics.ApplyChanges();
            _windowedSize = new Point(bounds.Width, bounds.Height);
        }
        finally
        {
            _applyingClientSize = false;
        }
    }

    private void ToggleExplore()
    {
        _explore = !_explore;
        _session.SetExplore(_explore);
        SaveSettings();
    }

    private void SaveSettings() =>
        SettingsFile.Save(_settingsPath, GameSettings.From(_viewport, _windowedSize.X, _windowedSize.Y, _graphics.IsFullScreen, _explore));

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        SaveSettings();
        base.OnExiting(sender, args);
    }

    private void HandlePhaseInput()
    {
        switch (_session.Phase)
        {
            case SessionPhase.TitleScreen:
                // Escape zuerst, damit das Beenden nicht als "beliebige Taste" im Menü landet.
                if (_inputAdapter.IsJustPressed(Keys.Escape)) _session.MenuQuit();
                else if (_inputAdapter.IsAnyKeyJustPressed()) _session.TitleAnyKey();
                break;
            case SessionPhase.Menu:
                if (_inputAdapter.IsJustPressed(Keys.Up)) _session.MenuUp();
                if (_inputAdapter.IsJustPressed(Keys.Down)) _session.MenuDown();
                if (_inputAdapter.IsJustPressed(Keys.Right)) _session.MenuNextCave();
                if (_inputAdapter.IsJustPressed(Keys.Left)) _session.MenuPreviousCave();
                // Start wie am Joystick-Feuerknopf ("PRESS SPACE TO PLAY"): Enter oder Leertaste.
                if (_inputAdapter.IsJustPressed(Keys.Enter)
                    || _inputAdapter.IsJustPressed(Keys.Space)) _session.MenuStart();
                // F5: kein Original-Menüpunkt, sondern der (unsichtbare) Zugang zum Testmodus
                // (GameSession.TestCaves) — bewusst ohne Legendenzeile auf dem Option-Screen.
                if (_inputAdapter.IsJustPressed(Keys.F5)) _session.MenuTestMode();
                // Escape geht hier nur eine Ebene zurück; beendet wird erst auf dem Titelbildschirm.
                if (_inputAdapter.IsJustPressed(Keys.Escape)) _session.MenuBack();
                break;
            case SessionPhase.TestMenu:
                // Gewählt wird allein mit dem Pfeil vor der Cave (TestMenuRenderer): Hoch/Runter
                // bewegt ihn, Enter oder Leertaste startet — wie im Option-Screen.
                if (_inputAdapter.IsJustPressed(Keys.Up)) _session.TestMenuPrevious();
                if (_inputAdapter.IsJustPressed(Keys.Down)) _session.TestMenuNext();
                if (_inputAdapter.IsJustPressed(Keys.Enter)
                    || _inputAdapter.IsJustPressed(Keys.Space)) _session.TestMenuStart();
                if (_inputAdapter.IsJustPressed(Keys.Escape)) _session.TestMenuBack();
                break;
            case SessionPhase.Playing:
                _inputAdapter.ApplyGameplay(_session.Input, _session.Cave?.Width ?? 1);
                // E: Cave-Explore umschalten (siehe ExploreMap). Bewusst KEINE Schalen-Taste
                // (InputAdapter.ShellKeys) wie F11/+/-, sondern eine Spielregel — sie gilt deshalb
                // nur während des Spiels.
                if (_inputAdapter.IsJustPressed(Keys.E)) ToggleExplore();
                if (_inputAdapter.IsJustPressed(Keys.Escape)) _session.EscapePressed();
                break;
            case SessionPhase.DeathPause:
            case SessionPhase.GameOverMessage:
                if (_inputAdapter.IsAnyKeyJustPressed()) _session.AnyKeyPressed();
                break;
            case SessionPhase.DemoPlaying:
                if (_inputAdapter.IsAnyKeyJustPressed()) _session.DemoInterrupted();
                break;
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        var (logicalWidth, logicalHeight) = GetLogicalSize();
        EnsureRenderTarget(logicalWidth, logicalHeight);

        GraphicsDevice.SetRenderTarget(_renderTarget);
        GraphicsDevice.Clear(Color.Black);
        DrawScene(gameTime.TotalGameTime.TotalSeconds, logicalWidth, logicalHeight);
        GraphicsDevice.SetRenderTarget(null);

        GraphicsDevice.Clear(Color.Black);

        // Bildschirm-Zoom: skalieren und im Fenster zentrieren; was übrig bleibt, ist schwarzer Rand.
        var scale = GetScale(logicalWidth, logicalHeight);
        var width = (int)(logicalWidth * scale);
        var height = (int)(logicalHeight * scale);
        var destination = new Rectangle(
            (GraphicsDevice.Viewport.Width - width) / 2,
            (GraphicsDevice.Viewport.Height - height) / 2,
            width,
            height);

        // Hochskaliert wird ganzzahlig, da hält PointClamp die Pixel scharf. Muss dagegen
        // heruntergerechnet werden (großes Sichtfenster auf kleinem Monitor, siehe GetScale), fiele
        // dabei jede zweite Pixelzeile weg — dort glättet LinearClamp.
        var sampler = scale >= 1f ? SamplerState.PointClamp : SamplerState.LinearClamp;
        _spriteBatch.Begin(samplerState: sampler);
        _spriteBatch.Draw(_renderTarget, destination, Color.White);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    /// <summary>Logische Auflösung der aktuellen Phase: die Menübildschirme sind BD1-Grafiken in
    /// fester Größe, das Spielfeld richtet sich nach dem gewählten Sichtfenster.</summary>
    private (int Width, int Height) GetLogicalSize()
    {
        if (_session.Phase is SessionPhase.TitleScreen or SessionPhase.Menu or SessionPhase.TestMenu
            || _session.Cave is null)
        {
            return (MenuWidth, MenuHeight);
        }

        return CaveRenderer.LogicalSize(_session.Camera.Viewport);
    }

    private void EnsureRenderTarget(int width, int height)
    {
        if (_renderTarget is not null && _renderTarget.Width == width && _renderTarget.Height == height)
        {
            return;
        }

        _renderTarget?.Dispose();
        _renderTarget = new RenderTarget2D(GraphicsDevice, width, height);
    }

    private void DrawScene(double totalSeconds, int logicalWidth, int logicalHeight)
    {
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        if (_session.Phase == SessionPhase.TitleScreen)
        {
            _titleRenderer.DrawTitle(_spriteBatch, totalSeconds);
        }
        else if (_session.Phase == SessionPhase.Menu)
        {
            _titleRenderer.DrawOptionScreen(_spriteBatch, _session, totalSeconds);
        }
        else if (_session.Phase == SessionPhase.TestMenu)
        {
            _testMenuRenderer.Draw(_spriteBatch, _session);
        }
        else if (_session.Cave is not null)
        {
            _caveRenderer.Draw(_spriteBatch, _session.Cave, _session.Camera, _session.State, _session.Input, _session.Clocks, _session.ScreenCover, _session.ExploreMap);

            // Statuszeile und Meldung sind 320 Pixel breit (40 BIOS-Zeichen) wie im Original und
            // bleiben deshalb auch bei größerem Sichtfenster mittig statt links zu kleben.
            var textLeft = (logicalWidth - MenuWidth) / 2;
            var statusText = BuildStatusLine();
            _font.DrawText(_spriteBatch, statusText, new Vector2(textLeft, 0), Color.White);

            if (_session.ShowGameOverMessage)
            {
                _font.DrawText(_spriteBatch, "GAME OVER", new Vector2(textLeft, logicalHeight / 2), Color.White);
            }
        }

        _spriteBatch.End();
    }

    /// <summary>Send_Message (src/GAME.CPP:34-48): vor dem Erscheinen "PLAYER 1, ..." Kopfzeile,
    /// danach die laufende Statusanzeige (Quote/Punkte/Diamanten/Zeit/Score).
    ///
    /// Der Ausgang setzt EntranceProgress zurück auf 0 (BOULDER.CPP:904) — die Kopfzeile darf danach
    /// trotzdem nicht wiederkommen, denn während der Bonuszählung muss man Zeit und Score laufen
    /// sehen. Im Original gibt Level_End() die Statuszeile dafür selbst aus (GAME.CPP:50-62) statt
    /// über Send_Message; hier deckt IsCaveEnded diesen Fall mit ab.</summary>
    private string BuildStatusLine()
    {
        var state = _session.State;
        var cave = _session.CurrentCaveData!;

        if (state.EntranceProgress < 99 && !state.IsCaveEnded)
        {
            return $"  P L A Y E R   1 ,   {state.Chances}  M E N   {cave.Letter} / {_session.DifficultyLevel}";
        }

        return $"{state.JewelQuota:D2}  -  {state.CurrentJewelPoints:D2}     " +
               $"{state.JewelsCollected:D2}      {state.CaveTimeRemaining:D3}         {state.Score:D6}";
    }

    private void SyncPalette()
    {
        // Die Atlas-Menüpalette braucht real nur noch der Testmodus (Titel-/Option-Screen haben
        // eigene Texturen mit festen Farben, siehe TitleRenderer) — Titel und Menü hier trotzdem
        // als Menü-Kontext zu behandeln hält den Kontextwechsel beim Spielstart unverändert einfach.
        if (_session.Phase is SessionPhase.TitleScreen or SessionPhase.Menu or SessionPhase.TestMenu)
        {
            if (_paletteContext != PaletteContext.Menu)
            {
                _spriteAtlas.ApplyPalette(TestMenuRenderer.MenuColors);
                _paletteContext = PaletteContext.Menu;
            }

            return;
        }

        if (_session.CurrentCaveData is null)
        {
            return;
        }

        var overrideColor = _session.State.PaletteColor0Override;
        var caveChanged = !ReferenceEquals(_session.CurrentCaveData, _lastPaletteCaveData);
        if (_paletteContext != PaletteContext.Cave || caveChanged || !Equals(overrideColor, _lastExitOverride))
        {
            // Kopie: die Farbe 0 wird für den blinkenden Ausgang ersetzt, die Cave-Daten bleiben unberührt.
            var palette = _session.CurrentCaveData.Colors.ToArray();
            if (overrideColor is { } color)
            {
                palette[0] = color;
            }

            _spriteAtlas.ApplyPalette(palette);
            _paletteContext = PaletteContext.Cave;
            _lastPaletteCaveData = _session.CurrentCaveData;
            _lastExitOverride = overrideColor;
        }
    }

    /// <summary>
    /// Der Maßstab, in dem die logische Zeichenfläche ins Fenster kommt.
    ///
    /// Passt sie hinein, wird sie GANZZAHLIG hochskaliert — Kachelpixel bleiben quadratisch und
    /// scharf. Für die Sichtfenster des Spiels ist das kein Zufall mehr: Die Zoomstufen sind aus
    /// genau dieser Fläche abgeleitet (ViewportSteps), der Maßstab kommt also glatt heraus und
    /// zwischen den Stufen bleibt nichts liegen. Herunterrechnen (bruchteilig, LinearClamp statt
    /// PointClamp) bleibt nur für den Fall, dass die Fläche kleiner ist als die kleinste Stufe —
    /// ein sehr kleines Fenster mit dem 320x200-Menüschirm oder dem Original-Sichtfenster.
    /// </summary>
    private float GetScale(int logicalWidth, int logicalHeight)
    {
        var fit = Math.Min(
            GraphicsDevice.Viewport.Width / (float)logicalWidth,
            GraphicsDevice.Viewport.Height / (float)logicalHeight);

        return fit >= 1f ? MathF.Floor(fit) : fit;
    }
}
