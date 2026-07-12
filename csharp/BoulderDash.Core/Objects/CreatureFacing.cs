namespace BoulderDash.Core.Objects;

/// <summary>
/// Blickrichtung einer Kreatur. Die Werte sind die Original-Richtungsbits 0x60 eines Kachelbytes und
/// stehen in derselben Reihenfolge wie die BDCFF-Attributbits (00/01/10/11) — dadurch ist eine
/// Vierteldrehung schlicht ein Schritt von 0x20.
/// </summary>
public enum CreatureFacing : byte
{
    Left = 0x00,
    Up = 0x20,
    Right = 0x40,
    Down = 0x60,
}

public static class CreatureFacingExtensions
{
    public static CreatureFacing TurnClockwise(this CreatureFacing facing) =>
        (CreatureFacing)(((byte)facing + 0x20) & 0x60);

    public static CreatureFacing TurnCounterClockwise(this CreatureFacing facing) =>
        (CreatureFacing)(((byte)facing - 0x20) & 0x60);

    /// <summary>Index-Versatz im Cave-Gitter, den ein Schritt in diese Richtung bedeutet.</summary>
    public static int Offset(this CreatureFacing facing, int caveWidth) => facing switch
    {
        CreatureFacing.Left => -1,
        CreatureFacing.Up => -caveWidth,
        CreatureFacing.Right => 1,
        _ => caveWidth,
    };
}
