using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Rockford. Er gräbt sich durch die Erde, sammelt Diamanten, schiebt Steine und zündet Kreaturen,
/// die ihm zu nahe kommen.
///
/// In Bewegung läuft der Laufzyklus (boulder_lauf(), BOULDER.CPP:611-646). Im Stand tappt er mit dem
/// Fuß und blinzelt (BDCFF 0006) — beides unabhängig voneinander, weil der C64 obere und untere
/// Körperhälfte getrennt steuert. Deshalb setzt sich sein Ruhebild aus zwei Hälften zusammen: Die
/// Sprite-Frames sind genau dafür gemacht — 14/15 ändern gegenüber dem Ruheframe 13 ausschließlich
/// die Augenpartie, 16/17 ausschließlich Arme und Füße. Ob er in der laufenden Sequenz überhaupt
/// blinzelt bzw. tappt, würfelt der GameTick aus (dort, nicht hier, damit die Ziehungen aus dem
/// gemeinsamen Zufallsstrom unabhängig davon bleiben, ob Rockford gerade auf dem Feld steht).
///
/// Das DOS-Original hatte die Ruheanimation nie fertiggestellt — boulder_wait() (BOULDER.CPP:648-663)
/// ist auskommentiert, seine Frames lagen brach.
/// </summary>
public sealed class RockfordObject : CaveObject
{
    /// <summary>Periode des Laufzyklus (wechsel_boulder): sechs Frames, 18-23.</summary>
    public const int WalkPeriod = 6;

    public override Element Element => Element.Rockford;

    /// <summary>Dieselbe Glyphe wie der Eingang: Rockford steht beim Laden noch nicht auf der Karte,
    /// er entsteht erst aus ihm (siehe EntranceObject).</summary>
    public override char MapGlyph => 'P';

    public override int DefaultFrame => 13;

    public override bool TriggersCreature => true;

    /// <summary>wechsel_boulder: Laufzyklus, läuft nur während einer aktiven Bewegungsrichtung.</summary>
    public byte WalkPhase { get; set; }

    /// <summary>Blinzelt er in der laufenden Achtersequenz? Gilt nur für diese eine Sequenz.</summary>
    public bool Blinking { get; set; }

    /// <summary>Tappt er mit dem Fuß? Anders als das Blinzeln ein Dauerzustand, der pro Sequenz nur
    /// mit 1/16 umschlägt.</summary>
    public bool Tapping { get; set; }

    public override void Animate(InputState input)
    {
        base.Animate(input);

        // Original nutzt hier (anders als bei den clk_*-Zählern!) zwei getrennte Anweisungen — erst
        // unbedingtes Inkrement, dann Prüfung des NEUEN Werts — das ergibt Periode 6, nicht 7.
        if (input.Direction != 0)
        {
            WalkPhase = (byte)((WalkPhase + 1) % WalkPeriod);
        }
    }

    public override TileAppearance Appearance(in RenderContext ctx)
    {
        if (ctx.Input.Direction != 0)
        {
            // Gespiegelt wird nur der Laufzyklus — auch das Original vertauscht die Sprite-Pixel
            // ausschließlich für die Frames 18-23 (boulder_lauf(), BOULDER.CPP:620-635); die nach
            // vorn gerichteten Ruheframes bleiben ungespiegelt.
            return new TileAppearance
            {
                Frame = 18 + WalkPhase,
                FlipHorizontally = ctx.Input.FacingLeft == 1,
            };
        }

        return new TileAppearance { Frame = EyeFrame, BottomFrame = FootFrame };
    }

    /// <summary>Augenpartie: Frame 13 offen, 14 halb, 15 geschlossen. Ein Blinzeln schließt die Augen
    /// einmal in der Mitte der Achtersequenz und öffnet sie wieder.</summary>
    private int EyeFrame => Blinking
        ? AnimationPhase switch
        {
            2 or 5 => 14,
            3 or 4 => 15,
            _ => 13,
        }
        : 13;

    /// <summary>Fußpartie: Frame 13 im Stillstand, sonst der Tappzyklus aus Frame 16 (Fuß unten) und
    /// 17 (Fuß angehoben) — zwei Schläge je Sequenz.</summary>
    private int FootFrame => Tapping
        ? (AnimationPhase / 2) % 2 == 0 ? 16 : 17
        : 13;
}
