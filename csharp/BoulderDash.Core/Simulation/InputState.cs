namespace BoulderDash.Core.Simulation;

/// <summary>
/// Bewegungssteuerung nach BD1 (BDCFF-Objektspezifikation 0006,
/// elmerproductions.com/sp/peterb/BDCFF/objects/0006.html): Gehalten werden alle gedrückten
/// Richtungen gleichzeitig; bei diagonaler Eingabe gilt "horizontal movement takes precedence over
/// the vertical". Innerhalb einer Achse (also links+rechts oder hoch+runter gleichzeitig) gewinnt
/// die zuletzt gedrückte Taste.
///
/// Das DOS-Original (Mov_Rockford, src/GAME.CPP:11-30) verfolgte stattdessen nur die ZULETZT
/// gedrückte Richtung: jedes Press überschrieb Richtung und Flags komplett. Hielt man eine Taste,
/// drückte kurz eine zweite und ließ die zweite wieder los, stoppte Rockford sofort — obwohl die
/// erste Taste noch gehalten wurde. Diese Eigenheit ist hier bewusst nicht mehr nachgebildet.
/// </summary>
public sealed class InputState
{
    private int _caveWidth = 1;

    private bool _right;
    private bool _left;
    private bool _down;
    private bool _up;

    /// <summary>Stichentscheid innerhalb einer Achse: zuletzt gedrückte Taste gewinnt.</summary>
    private bool _lastHorizontalWasRight;
    private bool _lastVerticalWasDown;

    /// <summary>Bewegungsrichtung als Index-Offset ins Cave-Gitter (0 = steht).</summary>
    public int Direction { get; private set; }

    /// <summary>Greifen: Rockford räumt die Zielzelle, ohne selbst umzuziehen. Das Original mischte
    /// dafür einen Modifikator (6) per XOR in die Kachelbytes — 0x86 ^ 6 == 0x80 machte aus "Rockford
    /// zieht um" ohne weiteren Code "Zelle wird leer" (GAME.CPP, Mov_Rockford).</summary>
    public bool IsGrabbing { get; private set; }

    /// <summary>status im Original: 0=zuletzt rechts, 1=zuletzt links (steuert die Sprite-Spiegelung).</summary>
    public byte FacingLeft { get; private set; }

    public void PressRight()
    {
        _right = true;
        _lastHorizontalWasRight = true;
        FacingLeft = 0;
        UpdateDirection();
    }

    public void PressLeft()
    {
        _left = true;
        _lastHorizontalWasRight = false;
        FacingLeft = 1;
        UpdateDirection();
    }

    public void PressDown(int caveWidth)
    {
        _caveWidth = caveWidth;
        _down = true;
        _lastVerticalWasDown = true;
        UpdateDirection();
    }

    public void PressUp(int caveWidth)
    {
        _caveWidth = caveWidth;
        _up = true;
        _lastVerticalWasDown = false;
        UpdateDirection();
    }

    public void PressGrab() => IsGrabbing = true;

    public void ReleaseGrab() => IsGrabbing = false;

    public void ReleaseRight()
    {
        _right = false;
        UpdateDirection();
    }

    public void ReleaseLeft()
    {
        _left = false;
        UpdateDirection();
    }

    public void ReleaseDown()
    {
        _down = false;
        UpdateDirection();
    }

    public void ReleaseUp()
    {
        _up = false;
        UpdateDirection();
    }

    /// <summary>Wie die level_laden-Rücksetzung von richtung/flags/kop (BOULDER.CPP:981-982,991):
    /// bei jedem Cave-Wechsel aufrufen, sonst bewegt sich Rockford in der neuen Cave sofort in die
    /// zuletzt gehaltene Richtung weiter (z.B. die Richtung, in der der vorige Ausgang betreten
    /// wurde). FacingLeft(status) wird im Original NICHT zurückgesetzt, bleibt also unverändert.</summary>
    public void ResetForNewCave()
    {
        _right = false;
        _left = false;
        _down = false;
        _up = false;
        IsGrabbing = false;
        Direction = 0;
    }

    private void UpdateDirection()
    {
        if (_right || _left)
        {
            // Waagerecht schlägt senkrecht.
            Direction = _right && (!_left || _lastHorizontalWasRight) ? 1 : -1;
            return;
        }

        if (_down || _up)
        {
            Direction = _down && (!_up || _lastVerticalWasDown) ? _caveWidth : -_caveWidth;
            return;
        }

        Direction = 0;
    }
}
