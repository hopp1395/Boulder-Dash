namespace BoulderDash.Core.Simulation;

/// <summary>
/// Bewegungssteuerung, transliteriert aus Mov_Rockford (src/GAME.CPP:11-30) und der
/// "if (flags&lt;0x10) flags=0,richtung=0;"-Regel aus der Eingabeschleife
/// (game_start/demo, BOULDER.CPP:396/370). Original-Quirk bewusst übernommen: jedes neue
/// "Drücken" überschreibt Flags/Richtung komplett — hält man eine Taste, drückt dann eine
/// zweite und lässt die zweite wieder los, bevor die erste losgelassen wird, stoppt die
/// Bewegung sofort, obwohl die erste Taste noch gehalten wird (das Original verfolgt nur die
/// zuletzt gedrückte Richtung, nicht alle gehaltenen Tasten gemeinsam).
///
/// Hinweis: Mov_Rockford selbst nutzt die realen Scancodes (0x48/0x50/0x4B/0x4D) direkt, nicht
/// die am Dateianfang von BOULDER.CPP irreführend benannten #defines RECHTS/LINKS (dort vertauscht)
/// — die Spielsteuerung ist davon nicht betroffen, nur eine mögliche Menü-Eigenheit (siehe M4).
/// </summary>
public sealed class InputState
{
    public int Direction { get; private set; }
    public int Flags { get; private set; }
    public byte GrabModifier { get; private set; }

    /// <summary>status im Original: 0=zuletzt rechts, 1=zuletzt links (steuert die Sprite-Spiegelung).</summary>
    public byte FacingLeft { get; private set; }

    public void PressRight()
    {
        Direction = 1;
        Flags = 0x40;
        FacingLeft = 0;
    }

    public void PressLeft()
    {
        Direction = -1;
        Flags = 0x10;
        FacingLeft = 1;
    }

    public void PressDown(int caveWidth)
    {
        Direction = caveWidth;
        Flags = 0x20;
    }

    public void PressUp(int caveWidth)
    {
        Direction = -caveWidth;
        Flags = 0x80;
    }

    public void PressGrab() => GrabModifier = 6;

    public void ReleaseGrab() => GrabModifier = 0;

    public void ReleaseRight() => Flags &= 0xBF;

    public void ReleaseLeft() => Flags &= 0xEF;

    public void ReleaseDown() => Flags &= 0xDF;

    public void ReleaseUp() => Flags &= 0x7F;

    /// <summary>Entspricht "if (flags&lt;0x10) flags=0,richtung=0;" — nach jeder Ereignisverarbeitung
    /// eines Ticks/Frames aufrufen.</summary>
    public void SettleIdleState()
    {
        if (Flags < 0x10)
        {
            Flags = 0;
            Direction = 0;
        }
    }
}
