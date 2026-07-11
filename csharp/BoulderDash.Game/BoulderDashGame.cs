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
    private MenuRenderer _menuRenderer = null!;
    private BiosFont _font = null!;
    private AudioPlayer _audioPlayer = null!;
    private InputAdapter _inputAdapter = null!;

    private GameSession _session = null!;

    private enum PaletteContext { None, Menu, Cave }

    private PaletteContext _paletteContext = PaletteContext.None;
    private CaveData? _lastPaletteCaveData;
    private Rgb? _lastExitOverride;
    private SessionPhase _lastPhase = SessionPhase.Menu;

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
        _spriteAtlas = new SpriteAtlas(GraphicsDevice, Path.Combine(assets, "Sprites"));
        _caveRenderer = new CaveRenderer(_spriteAtlas);
        _font = new BiosFont(GraphicsDevice);
        _menuRenderer = new MenuRenderer(_spriteAtlas, _font);
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

        if (_session.Phase == SessionPhase.Menu)
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
            case SessionPhase.Menu:
                if (_inputAdapter.IsJustPressed(Keys.Up)) _session.MenuUp();
                if (_inputAdapter.IsJustPressed(Keys.Down)) _session.MenuDown();
                if (_inputAdapter.IsJustPressed(Keys.Right)) _session.MenuNextCave();
                if (_inputAdapter.IsJustPressed(Keys.Left)) _session.MenuPreviousCave();
                if (_inputAdapter.IsJustPressed(Keys.F1)) _session.MenuStart();
                if (_inputAdapter.IsJustPressed(Keys.F2)) _session.MenuDemo();
                // F3 (Hilfe) ist bereits im Original ohne Funktion.
                if (_inputAdapter.IsJustPressed(Keys.F4)) _session.MenuQuit();
                break;
            case SessionPhase.Playing:
                _inputAdapter.ApplyGameplay(_session.Input, _session.Cave?.Width ?? 1);
                if (_inputAdapter.IsJustPressed(Keys.Escape)) _session.EscapePressed();
                break;
            case SessionPhase.DeathPause:
            case SessionPhase.GameOverMessage:
                if (_inputAdapter.IsAnyKeyJustPressed()) _session.AnyKeyPressed();
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

        if (_session.Phase == SessionPhase.Menu)
        {
            _menuRenderer.Draw(_spriteBatch, _session);
        }
        else if (_session.Cave is not null)
        {
            _caveRenderer.Draw(_spriteBatch, _session.Cave, _session.Camera, _session.State, _session.Input, _session.Clocks, _session.Dissolve);

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
    /// danach die laufende Statusanzeige (Quote/Punkte/Diamanten/Zeit/Score).</summary>
    private string BuildStatusLine()
    {
        var state = _session.State;
        var cave = _session.CurrentCaveData!;

        if (state.EntranceProgress < 99)
        {
            return $"  P L A Y E R   1 ,   {state.Chances}  M E N   {cave.Letter} / {_session.DifficultyLevel}";
        }

        return $"{state.JewelQuota:D2}  -  {state.CurrentJewelPoints:D2}     " +
               $"{state.JewelsCollected:D2}      {state.CaveTimeRemaining:D3}         {state.Score:D6}";
    }

    private void SyncPalette()
    {
        if (_session.Phase == SessionPhase.Menu)
        {
            if (_paletteContext != PaletteContext.Menu)
            {
                _spriteAtlas.ApplyPalette(Palette.BuildCavePalette(MenuRenderer.MenuBaseColors));
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
