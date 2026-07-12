using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>Leerraum. Trägt trotzdem Zustand: Eine Leerzelle, die in diesem Scan gerade erst
/// freigeräumt wurde (<see cref="CaveObject.Scanned"/>), ist noch kein freier Platz — sonst würde
/// ein Objekt der eigenen Bewegung im selben Scan hinterherfallen.</summary>
public sealed class EmptyObject : CaveObject
{
    public EmptyObject(Cave? cave = null)
        : base(cave)
    {
    }

    public override Element Element => Element.Empty;

    public override char MapGlyph => ' ';

    public override int DefaultFrame => 0;

    public override bool IsFreeSpace => !Scanned;

    public override bool CanAmoebaGrowInto => !Scanned;
}
