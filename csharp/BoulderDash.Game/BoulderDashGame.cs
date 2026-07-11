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
    private Texture2D _pixel = null!;

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
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
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
        DrawTestPattern();
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
    /// Platzhalter-Testbild für M0 (Gerüst): belegt, dass RenderTarget und Integer-Skalierung
    /// funktionieren. Wird in M1 durch CaveRenderer ersetzt.
    /// </summary>
    private void DrawTestPattern()
    {
        var farben = new[] { Color.White, Color.Red, Color.Yellow, Color.Blue };
        var breite = LogicalWidth / farben.Length;

        _spriteBatch.Begin();
        for (var i = 0; i < farben.Length; i++)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(i * breite, 0, breite, LogicalHeight), farben[i]);
        }
        _spriteBatch.End();
    }

    private int GetIntegerScale()
    {
        var scaleX = GraphicsDevice.Viewport.Width / LogicalWidth;
        var scaleY = GraphicsDevice.Viewport.Height / LogicalHeight;
        return System.Math.Max(1, System.Math.Min(scaleX, scaleY));
    }
}
