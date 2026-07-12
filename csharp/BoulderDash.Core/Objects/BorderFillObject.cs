using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Der Rand-Füllstein (MASK_SLAUF), mit dem der Bildschirm auf- und zugedeckt wird. Sein Sprite ist
/// als einziges 24 statt 16 Zeilen hoch: Das 16-Zeilen-Fenster wandert im Animationstakt hindurch,
/// wodurch das Mauermuster senkrecht durchläuft. Deshalb ist es kein Frame-Wechsel, sondern ein
/// Zeilenversatz (buffer[MASK_SLAUF] = z_zeiger[76] + wechsel_vier*16 ist im Original
/// Byte-Zeiger-Arithmetik auf dem Rohsprite, BOULDER.CPP:604).
///
/// Kommt in keiner Cave-Datei vor — die Verdeckung legt sich nur über das Gitter.
/// </summary>
public sealed class BorderFillObject : CaveObject
{
    public BorderFillObject(Cave cave)
        : base(cave)
    {
    }

    public override Element Element => Element.BorderFill;

    public override int DefaultFrame => 76;

    public override TileAppearance Appearance(in RenderContext ctx) =>
        new() { Frame = DefaultFrame, RowOffset = AnimationPhase };
}
