using BoulderDash.Core.Data;
using BoulderDash.Core.Simulation;
using BoulderDash.Game.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using XnaGame = Microsoft.Xna.Framework.Game;

namespace BoulderDash.Game;

/// <summary>
/// Haupteinstiegspunkt der MonoGame-Anwendung. Rendert intern auf ein 320x200-RenderTarget
/// (VGA-Mode-13h-Auflösung des Originals) und skaliert es beim Zeichnen ganzzahlig auf die
/// Fenstergröße hoch (siehe copy64() in src/BOULDER.CPP als Vorbild für die Zielauflösung).
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
    private BiosFont _font = null!;
    private CaveData _cave = null!;

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
        _spriteAtlas = new SpriteAtlas(GraphicsDevice, Path.Combine(assets, "SPRITES.BIN"));
        _caveRenderer = new CaveRenderer(_spriteAtlas);
        _font = new BiosFont(GraphicsDevice);

        var caves = CaveFile.LoadAll(Path.Combine(assets, "LEVEL.BIN"));
        _cave = caves[0]; // Cave A
        _spriteAtlas.ApplyPalette(Palette.BuildCavePalette(_cave.BaseColors));
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // Erst in logischer Auflösung zeichnen ...
        GraphicsDevice.SetRenderTarget(_renderTarget);
        GraphicsDevice.Clear(Color.Black);
        DrawCaveStandbild();
        GraphicsDevice.SetRenderTarget(null);

        // ... dann mit Punktfilterung ganzzahlig auf das Fenster skalieren.
        GraphicsDevice.Clear(Color.Black);
        var scale = GetIntegerScale();
        var destination = new Rectangle(0, 0, LogicalWidth * scale, LogicalHeight * scale);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_renderTarget, destination, Color.White);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    /// <summary>
    /// M1-Standbild: Cave A im Ladezustand (keine Animation/Simulation, siehe CaveRenderer)
    /// plus Statuszeile im "vor Levelaufbau"-Format von Send_Message (src/GAME.CPP:34-48).
    /// </summary>
    private void DrawCaveStandbild()
    {
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _caveRenderer.Draw(_spriteBatch, _cave);

        const byte chances = 3; // Original: leben=3 (Start_menu, BOULDER.CPP:302)
        const byte difficultyLevel = 1; // Original: levelnr=1 (Standardwert)
        var statusText = $"  P L A Y E R   1 ,   {chances}  M E N   {_cave.Letter} / {difficultyLevel}";
        _font.DrawText(_spriteBatch, statusText, Vector2.Zero, Color.White);

        _spriteBatch.End();
    }

    private int GetIntegerScale()
    {
        var scaleX = GraphicsDevice.Viewport.Width / LogicalWidth;
        var scaleY = GraphicsDevice.Viewport.Height / LogicalHeight;
        return System.Math.Max(1, System.Math.Min(scaleX, scaleY));
    }
}
