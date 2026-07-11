namespace BoulderDash.Core.Simulation;

/// <summary>
/// Sichtfensterposition (bild_xpos/ypos) und Scroll-Rest (relx/rely), src/BOULDER.CPP:78,116-117.
/// Scrolling ist original kachelweise (1 Kachel pro Tick, nicht pixelweise) — bewusst so belassen.
/// </summary>
public sealed class Camera
{
    public int X { get; set; }
    public int Y { get; set; }
    public sbyte Relx { get; set; }
    public sbyte Rely { get; set; }

    /// <summary>Ein Scroll-Schritt plus Klemmung, wie in der Game-ISR (BOULDER.CPP:229-237).</summary>
    public void Step(int caveWidth, int caveHeight)
    {
        if (Relx > 0)
        {
            Relx--;
            X++;
        }

        if (Relx < 0)
        {
            Relx++;
            X--;
        }

        if (Rely > 0)
        {
            Rely--;
            Y++;
        }

        if (Rely < 0)
        {
            Rely++;
            Y--;
        }

        if (X > caveWidth - 20)
        {
            X = caveWidth - 20;
        }

        if (X < 0)
        {
            X = 0;
        }

        if (Y > caveHeight - 12)
        {
            Y = caveHeight - 12;
        }

        if (Y < 0)
        {
            Y = 0;
        }
    }

    public void ResetTo(int x, int y)
    {
        X = x;
        Y = y;
        Relx = 0;
        Rely = 0;
    }
}
