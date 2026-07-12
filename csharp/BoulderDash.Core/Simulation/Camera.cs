namespace BoulderDash.Core.Simulation;

/// <summary>
/// Sichtfensterposition (bild_xpos/ypos) und Scroll-Rest (relx/rely), src/BOULDER.CPP:78,116-117.
/// Scrolling ist original kachelweise (1 Kachel pro Tick, nicht pixelweise) — bewusst so belassen.
/// Die Größe des Sichtfensters ist im Gegensatz zum Original einstellbar (siehe ViewportSize);
/// mit dem Standardwert 20x12 verhält sich die Kamera exakt wie dort.
/// </summary>
public sealed class Camera
{
    public int X { get; set; }
    public int Y { get; set; }
    public sbyte Relx { get; set; }
    public sbyte Rely { get; set; }

    /// <summary>Sichtfenstergröße in Kacheln (Spielflächen-Zoom); Original 20x12.</summary>
    public ViewportSize Viewport { get; set; } = ViewportSize.Original;

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

        Clamp(caveWidth, caveHeight);
    }

    /// <summary>Hält das Sichtfenster in der Cave (BOULDER.CPP:233-237). Ist das Sichtfenster größer
    /// als die Cave (möglich seit dem Spielflächen-Zoom, z. B. bei den 20x12-Intermissions), wird die
    /// Obergrenze negativ und die Kamera steht auf 0 — die Cave wird dann beim Zeichnen zentriert.</summary>
    public void Clamp(int caveWidth, int caveHeight)
    {
        X = Math.Clamp(X, 0, Math.Max(0, caveWidth - Viewport.Columns));
        Y = Math.Clamp(Y, 0, Math.Max(0, caveHeight - Viewport.Rows));
    }

    /// <summary>Setzt das Sichtfenster so, dass die angegebene Kachel (der Eingang) möglichst mittig
    /// liegt — beim Cave-Start. Lag früher als CameraStartX/Y in der Cave-Datei-Auswertung; seit das
    /// Sichtfenster zur Laufzeit veränderbar ist, muss es hier gerechnet werden.</summary>
    public void CenterOn(int col, int row, int caveWidth, int caveHeight)
    {
        X = col - (Viewport.Columns / 2);
        Y = row - (Viewport.Rows / 2);
        Relx = 0;
        Rely = 0;
        Clamp(caveWidth, caveHeight);
    }

    public void ResetTo(int x, int y)
    {
        X = x;
        Y = y;
        Relx = 0;
        Rely = 0;
    }
}
