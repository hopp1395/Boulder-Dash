namespace BoulderDash.Core.Simulation;

/// <summary>
/// Transliteration von level_in() (src/BOULDER.CPP:569-589): Auflöse-Effekt beim Cave-Start.
/// Arbeitet NICHT auf der Cave selbst, sondern auf einer 240-Zellen-Sichtfenster-Maske (wie das
/// Original auf bsp/newmask), die CaveRenderer beim Zeichnen als Overlay abfragt — im Original
/// wird die Maske pro Tick frisch über die gerade kopierte Sichtfenster-Momentaufnahme gelegt.
///
/// Original-Eigenheiten bewusst übernommen:
/// - newmask hat 241 statt 240 Einträge: "j=(rand()%240)+1" kann Index 240 erzeugen (Off-by-one-Bug).
/// - Die lokale Variable j wird bei JEDEM Aufruf neu auf 0 initialisiert (kein Zustand über Ticks
///   hinweg), wodurch der erste von vier pro Tick geräumten Zellen oft deterministisch statt
///   zufällig ist (siehe Tick()).
/// </summary>
public sealed class Dissolve
{
    private readonly byte[] _mask = new byte[241];
    private readonly BorlandRandom _random;

    public Dissolve(BorlandRandom random)
    {
        _random = random;
        Array.Fill(_mask, (byte)15);
    }

    /// <summary>Nur aufrufen solange anfang_var&lt;65 (siehe GameTick), wie im Original
    /// (`if ((anfang_var&lt;65)&amp;&amp;(level_ende!=0xFF)) level_in();`).</summary>
    public void Tick(byte entranceProgress)
    {
        if (entranceProgress < 5)
        {
            Array.Fill(_mask, (byte)15);
            return;
        }

        var j = 0;
        for (var z = 0; z < 4; z++)
        {
            var i = 0;
            while (_mask[j] == 0)
            {
                j = (_random.Next() % 240) + 1;
                if (i++ > 240)
                {
                    break;
                }
            }

            _mask[j] = 0;
        }
    }

    /// <summary>Index 0..239 im 20x12-Sichtfenster (zeilenweise, wie bsp[]).</summary>
    public bool IsCovered(int viewportIndex) => _mask[viewportIndex] == 15;
}
