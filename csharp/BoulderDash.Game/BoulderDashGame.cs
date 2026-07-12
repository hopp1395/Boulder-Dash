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
/// Haupteinstiegspunkt der MonoGame-Anwendung. Rendert intern auf ein 320x200-RenderTarget
/// (VGA-Mode-13h-Auflösung des Originals) und skaliert es beim Zeichnen ganzzahlig auf die
/// Fenstergröße hoch. Delegiert Menü-/Spielablauf komplett an GameSession (Core) und wählt hier
/// nur, welcher Renderer je nach SessionPhase zum Einsatz kommt.
/// </summary>
public class BoulderDashGame : XnaGame
{
    private const int LogicalWidth = 320;
    private const int LogicalHeight = 200;

    private readonly GraphicsDeviceManager _graphics;
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

    private enum PaletteContext { None, Menu, Cave }

    private PaletteContext _paletteContext = PaletteContext.None;
    private CaveData? _lastPaletteCaveData;
    private Rgb? _lastExitOverride;
    private SessionPhase _lastPhase = SessionPhase.TitleScreen;

    public BoulderDashGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = LogicalWidth * 3,
            PreferredBackBufferHeight = LogicalHeight * 3,
        };
        IsMouseVisible = false;
    }

    protected override void Initialize()
    {
        base.Initialize();
        _renderTarget = new RenderTarget2D(GraphicsDevice, LogicalWidth, LogicalHeight);
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
        _testMenuRenderer = new TestMenuRenderer(_spriteAtlas, _font);
        _audioPlayer = new AudioPlayer();
        _inputAdapter = new InputAdapter();

        var caves = new CaveTextRepository(Path.Combine(assets, "Caves"));
        var demoSteps = DemoTextFile.Load(Path.Combine(assets, "demo.txt"));
        _session = new GameSession(caves, demoSteps);

        SyncPalette();
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        _inputAdapter.Update(keyboard);

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

    private void HandlePhaseInput()
    {
        switch (_session.Phase)
        {
            case SessionPhase.TitleScreen:
                // F4 zuerst, damit das Beenden nicht als "beliebige Taste" im Menü landet.
                if (_inputAdapter.IsJustPressed(Keys.F4)) _session.MenuQuit();
                else if (_inputAdapter.IsAnyKeyJustPressed()) _session.TitleAnyKey();
                break;
            case SessionPhase.Menu:
                if (_inputAdapter.IsJustPressed(Keys.Up)) _session.MenuUp();
                if (_inputAdapter.IsJustPressed(Keys.Down)) _session.MenuDown();
                if (_inputAdapter.IsJustPressed(Keys.Right)) _session.MenuNextCave();
                if (_inputAdapter.IsJustPressed(Keys.Left)) _session.MenuPreviousCave();
                // Start wie am Joystick-Feuerknopf ("PRESS BUTTON TO PLAY"): F1, Enter oder Leertaste.
                if (_inputAdapter.IsJustPressed(Keys.F1)
                    || _inputAdapter.IsJustPressed(Keys.Enter)
                    || _inputAdapter.IsJustPressed(Keys.Space)) _session.MenuStart();
                if (_inputAdapter.IsJustPressed(Keys.F4)) _session.MenuQuit();
                // F5: kein Original-Menüpunkt, sondern der (unsichtbare) Zugang zum Testmodus
                // (GameSession.TestCaves) — bewusst ohne Legendenzeile auf dem Option-Screen.
                if (_inputAdapter.IsJustPressed(Keys.F5)) _session.MenuTestMode();
                if (_inputAdapter.IsJustPressed(Keys.Escape)) _session.MenuBack();
                break;
            case SessionPhase.TestMenu:
                if (_inputAdapter.IsJustPressed(Keys.Up)) _session.TestMenuPrevious();
                if (_inputAdapter.IsJustPressed(Keys.Down)) _session.TestMenuNext();
                // Direktwahl über die Zifferntasten: 1-9, die zehnte Cave liegt auf der 0. Darüber
                // hinaus bleibt nur die Auswahl über Hoch/Runter.
                for (var i = 0; i < GameSession.TestCaves.Count && i < 9; i++)
                {
                    if (_inputAdapter.IsJustPressed(Keys.D1 + i)) _session.TestMenuSelect(i);
                }

                if (_inputAdapter.IsJustPressed(Keys.D0)) _session.TestMenuSelect(9);

                if (_inputAdapter.IsJustPressed(Keys.F1) || _inputAdapter.IsJustPressed(Keys.Enter)) _session.TestMenuStart();
                if (_inputAdapter.IsJustPressed(Keys.Escape)) _session.TestMenuBack();
                break;
            case SessionPhase.Playing:
                _inputAdapter.ApplyGameplay(_session.Input, _session.Cave?.Width ?? 1);
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
        GraphicsDevice.SetRenderTarget(_renderTarget);
        GraphicsDevice.Clear(Color.Black);
        DrawScene();
        GraphicsDevice.SetRenderTarget(null);

        GraphicsDevice.Clear(Color.Black);
        var scale = GetIntegerScale();
        var destination = new Rectangle(0, 0, LogicalWidth * scale, LogicalHeight * scale);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_renderTarget, destination, Color.White);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void DrawScene()
    {
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        if (_session.Phase == SessionPhase.TitleScreen)
        {
            _titleRenderer.DrawTitle(_spriteBatch);
        }
        else if (_session.Phase == SessionPhase.Menu)
        {
            _titleRenderer.DrawOptionScreen(_spriteBatch, _session);
        }
        else if (_session.Phase == SessionPhase.TestMenu)
        {
            _testMenuRenderer.Draw(_spriteBatch, _session);
        }
        else if (_session.Cave is not null)
        {
            _caveRenderer.Draw(_spriteBatch, _session.Cave, _session.Camera, _session.State, _session.Input, _session.Clocks, _session.ScreenCover);

            var statusText = BuildStatusLine();
            _font.DrawText(_spriteBatch, statusText, Vector2.Zero, Color.White);

            if (_session.ShowGameOverMessage)
            {
                _font.DrawText(_spriteBatch, "GAME OVER", new Vector2(0, 100), Color.White);
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
                _spriteAtlas.ApplyPalette(Palette.BuildCavePalette(TestMenuRenderer.MenuBaseColors));
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
            var palette = Palette.BuildCavePalette(_session.CurrentCaveData.BaseColors);
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

    private int GetIntegerScale()
    {
        var scaleX = GraphicsDevice.Viewport.Width / LogicalWidth;
        var scaleY = GraphicsDevice.Viewport.Height / LogicalHeight;
        return System.Math.Max(1, System.Math.Min(scaleX, scaleY));
    }
}
