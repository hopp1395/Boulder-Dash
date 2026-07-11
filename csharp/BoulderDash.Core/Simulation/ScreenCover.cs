namespace BoulderDash.Core.Simulation;

public enum ScreenCoverPhase
{
    Idle,
    Uncovering,
    Covering,
}

/// <summary>
/// Die Verdeckung der Cave beim Start und beim Ende ("uncovering and covering the screen"):
/// Die Cave liegt zunächst komplett unter einer animierten Stahlwand (Element.BorderFill, das
/// MASK_SLAUF-Sprite) und wird zeilenweise-zufällig freigelegt; am Cave-Ende läuft dasselbe
/// rückwärts.
///
/// BEWUSSTE ABWEICHUNG vom DOS-Original: Dort deckt level_in() (BOULDER.CPP:569-589) pro Tick
/// vier zufällige Zellen der 240er-SICHTFENSTER-Maske auf, und ein Zudecken gibt es gar nicht
/// (level_out() scrollt den Bildschirm stattdessen hoch). Nachgebildet ist hier das
/// C64-BD1-Verhalten:
///
///   loop 69 times
///     foreach line in 1..22            (die Zeilen der CAVE, nicht des Sichtfensters —
///       uncover a random position      auf dem C64 ist die ganze Cave sichtbar)
///   uncover entire screen
///
/// Die Maske liegt darum in Cave-Koordinaten; der Port zeigt davon sein scrollendes
/// 20x12-Fenster. Nach den 69 Runden sind je Zeile im Schnitt ~82% der Zellen frei, den Rest
/// räumt das abschließende Vollaufdecken.
///
/// Der Zufall kommt aus derselben BorlandRandom-Instanz wie die Physik (ein einziger
/// deterministischer Strom, wie im Original, wo level_in() und regel() sich rand() teilen) —
/// der Strom verschiebt sich gegenüber DOS aber ohnehin, da pro Tick 22 statt 4 Zufallszahlen
/// gezogen werden.
/// </summary>
public sealed class ScreenCover
{
    /// <summary>"loop 69 times" — eine Runde pro GameTick (bei ~20 Ticks/s also ~3,5 s).</summary>
    public const int Iterations = 69;

    private readonly BorlandRandom _random;

    private bool[] _covered = [];
    private int _width;
    private int _height;
    private int _iteration;

    public ScreenCover(BorlandRandom random)
    {
        _random = random;
    }

    public ScreenCoverPhase Phase { get; private set; } = ScreenCoverPhase.Idle;

    /// <summary>Läuft gerade eine Auf- oder Zudeck-Animation? (Solange spielt der Uncover-Sound.)</summary>
    public bool IsActive => Phase != ScreenCoverPhase.Idle;

    /// <summary>Cave-Start: alles verdecken, danach Runde für Runde freilegen.</summary>
    public void BeginUncover(int width, int height)
    {
        _width = width;
        _height = height;
        _covered = new bool[width * height];
        Array.Fill(_covered, true);
        _iteration = 0;
        Phase = ScreenCoverPhase.Uncovering;
    }

    /// <summary>Cave-Ende: sichtbare Cave Runde für Runde wieder zudecken.</summary>
    public void BeginCover()
    {
        if (_covered.Length == 0)
        {
            return;
        }

        Array.Fill(_covered, false);
        _iteration = 0;
        Phase = ScreenCoverPhase.Covering;
    }

    /// <summary>Eine Runde der äußeren Schleife: je Cave-Zeile eine zufällige Spalte umschalten.
    /// Nach der letzten Runde die ganze Cave auf den Zielzustand setzen ("uncover entire screen")
    /// und die Animation beenden.</summary>
    public void Tick()
    {
        if (Phase == ScreenCoverPhase.Idle)
        {
            return;
        }

        var target = Phase == ScreenCoverPhase.Covering;

        for (var y = 0; y < _height; y++)
        {
            var x = _random.Next() % _width;
            _covered[(y * _width) + x] = target;
        }

        if (++_iteration < Iterations)
        {
            return;
        }

        Array.Fill(_covered, target);
        Phase = ScreenCoverPhase.Idle;
    }

    public bool IsCovered(int x, int y)
    {
        if (_covered.Length == 0)
        {
            return false;
        }

        return _covered[(y * _width) + x];
    }
}
