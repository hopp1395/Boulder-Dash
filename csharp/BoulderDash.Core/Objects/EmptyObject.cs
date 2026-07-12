using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>Leerraum. Trägt trotzdem Zustand: Eine Leerzelle, die in diesem Scan gerade erst
/// freigeräumt wurde (<see cref="CaveObject.ScannedThisFrame"/>), ist noch kein freier Platz — sonst würde
/// ein Objekt der eigenen Bewegung im selben Scan hinterherfallen.
///
/// Nicht versiegelt, weil das Nichts außerhalb der Höhle (<see cref="VoidObject"/>) darauf aufbaut:
/// Es ist für die Physik derselbe Leerraum und unterscheidet sich nur in der Darstellung.</summary>
public class EmptyObject : CaveObject
{
    public EmptyObject(Cave cave)
        : base(cave)
    {
    }

    public override Element Element => Element.Empty;

    public override char MapGlyph => ' ';

    public override int DefaultFrame => 0;

    public override bool IsFreeSpace => !ScannedThisFrame;

    /// <summary>Durch Leerraum fällt es hindurch. Anders als beim Abrollen genügt hier auch eine
    /// Leerzelle, die in diesem Scan schon verarbeitet wurde — das Original maskiert an dieser Stelle
    /// bewusst nur die Element-ID heraus und übersieht das Verarbeitet-Flag.</summary>
    public override void ReceiveFalling(FallingObject faller) => faller.FallThrough();
}
