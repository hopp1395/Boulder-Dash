using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>Gemauerte Wand. Abgerundet (BDCFF 0000): Steine und Diamanten rollen von ihr ab, wenn
/// daneben Platz ist. Sprengbar.</summary>
public sealed class WallObject : CaveObject
{
    public WallObject(Cave cave)
        : base(cave)
    {
    }

    public override Element Element => Element.Wall;

    public override char MapGlyph => 'w';

    public override int DefaultFrame => 11;

    /// <summary>Die Mauer ist rund (BDCFF 0000): Was auf sie fällt, rollt zur Seite ab, wenn dort
    /// Platz ist.</summary>
    public override void ReceiveFalling(FallingObject faller) => faller.RollOff();
}
