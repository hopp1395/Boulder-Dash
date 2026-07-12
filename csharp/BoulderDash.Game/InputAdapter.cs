using BoulderDash.Core.Simulation;
using Microsoft.Xna.Framework.Input;

namespace BoulderDash.Game;

/// <summary>
/// Wandelt MonoGame-Tastaturzustände in diskrete Druck-/Loslass-Ereignisse um (Kantenerkennung
/// zwischen zwei Frames), da das Original rohe Scancode-Ereignisse verarbeitet (Mov_Rockford,
/// src/GAME.CPP:11-30; Start_menu, BOULDER.CPP:291-329) statt eines Zustands-Snapshots.
/// <see cref="Update"/> muss einmal pro Frame vor allen Abfragen aufgerufen werden.
/// </summary>
public sealed class InputAdapter
{
    /// <summary>Bildschirm-/Spielflächen-Zoom sind Bedienung der Schale, keine Spieleingabe: sie
    /// dürfen weder den Titelbildschirm weiterschalten noch die Demo oder eine Pause abbrechen und
    /// zählen deshalb nicht als "beliebige Taste".</summary>
    public static readonly Keys[] ShellKeys =
        [Keys.F11, Keys.OemPlus, Keys.OemMinus, Keys.Add, Keys.Subtract];

    private KeyboardState _previous;
    private KeyboardState _current;

    public void Update(KeyboardState current)
    {
        _previous = _current;
        _current = current;
    }

    public bool IsJustPressed(Keys key) => _current.IsKeyDown(key) && !_previous.IsKeyDown(key);

    /// <summary>True, sobald eine der angegebenen Tasten neu gedrückt wurde (z. B. die
    /// gleichwertigen Zoom-Tasten auf Haupt- und Zehnerblock).</summary>
    public bool IsAnyJustPressed(params Keys[] keys)
    {
        foreach (var key in keys)
        {
            if (IsJustPressed(key))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsAnyKeyJustPressed()
    {
        foreach (var key in _current.GetPressedKeys())
        {
            if (Array.IndexOf(ShellKeys, key) >= 0)
            {
                continue;
            }

            if (!_previous.IsKeyDown(key))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Spielsteuerung während Playing: Pfeiltasten + Strg (Greifen).</summary>
    public void ApplyGameplay(InputState input, int caveWidth)
    {
        HandleKey(Keys.Right, () => input.PressRight(), input.ReleaseRight);
        HandleKey(Keys.Left, () => input.PressLeft(), input.ReleaseLeft);
        HandleKey(Keys.Down, () => input.PressDown(caveWidth), input.ReleaseDown);
        HandleKey(Keys.Up, () => input.PressUp(caveWidth), input.ReleaseUp);
        HandleKey(Keys.LeftControl, input.PressGrab, input.ReleaseGrab);
    }

    private void HandleKey(Keys key, Action onPress, Action onRelease)
    {
        var isDown = _current.IsKeyDown(key);
        var wasDown = _previous.IsKeyDown(key);

        if (isDown && !wasDown)
        {
            onPress();
        }
        else if (!isDown && wasDown)
        {
            onRelease();
        }
    }
}
