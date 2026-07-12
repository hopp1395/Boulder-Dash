using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Rockford. Er gräbt sich durch die Erde, sammelt Diamanten, schiebt Steine, verlässt die Cave durch
/// den Ausgang — und zündet Kreaturen, die ihm zu nahe kommen.
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

    public RockfordObject(Cave cave)
        : base(cave)
    {
    }

    public override Element Element => Element.Rockford;

    /// <summary>Dieselbe Glyphe wie der Eingang: Rockford steht beim Laden noch nicht auf der Karte,
    /// er entsteht erst aus ihm (siehe EntranceObject).</summary>
    public override char MapGlyph => 'P';

    public override int DefaultFrame => 13;

    public override bool TriggersCreature => true;

    /// <summary>Fällt ihm ein Stein auf den Kopf, ist es aus mit ihm.</summary>
    public override void ReceiveFalling(FallingObject faller) => faller.Crush(this);

    /// <summary>wechsel_boulder: Laufzyklus, läuft nur während einer aktiven Bewegungsrichtung.</summary>
    public byte WalkPhase { get; set; }

    /// <summary>Blinzelt er in der laufenden Achtersequenz? Gilt nur für diese eine Sequenz.</summary>
    public bool Blinking { get; set; }

    /// <summary>Tappt er mit dem Fuß? Anders als das Blinzeln ein Dauerzustand, der pro Sequenz nur
    /// mit 1/16 umschlägt.</summary>
    public bool Tapping { get; set; }

    public override void NextFrame()
    {
        base.NextFrame();

        // Original nutzt hier (anders als bei den clk_*-Zählern!) zwei getrennte Anweisungen — erst
        // unbedingtes Inkrement, dann Prüfung des NEUEN Werts — das ergibt Periode 6, nicht 7.
        if (Cave.Input.Direction != 0)
        {
            WalkPhase = (byte)((WalkPhase + 1) % WalkPeriod);
        }
    }

    /// <summary>
    /// Rockfords Zug (:890-923). Zuerst die vier Kamerabedingungen — sie setzen nur das Scroll-Ziel
    /// und beeinflussen die Bewegung nicht.
    ///
    /// Im DOS-Original hing die Bewegungsverarbeitung durch ein Dangling-Else an der vierten
    /// Bedingung: Löste Rockford den Aufwärtsscroll aus, blieb seine Bewegung den ganzen Scan über
    /// aus — er hakte sichtbar. Ein reiner Programmierfehler ohne BD1-Entsprechung, hier behoben.
    /// </summary>
    public override void Interact()
    {
        if (ScannedThisFrame)
        {
            return;
        }

        // stat: Rockford lebt, denn er wurde in diesem Scan gefunden.
        Cave.State.Stat = 0;

        ScrollCamera();

        var input = Cave.Input;
        var targetIndex = Index + input.Direction;
        var target = Cave.Get(targetIndex);

        // Eine in diesem Scan schon verarbeitete Zelle ist unpassierbar. Im Original leistet das das
        // Verarbeitet-Bit, das die Zielmaske (anders als alle anderen) NICHT ausblendet — ein Detail,
        // das leicht zu übersehen ist, aber Rockford daran hindert, einer gerade geräumten Zelle
        // hinterherzuziehen. Steht er still, ist das Ziel er selbst und passiert nichts.
        if (target.ScannedThisFrame)
        {
            return;
        }

        var state = Cave.State;

        switch (target)
        {
            case EscapeDoorObject when state.JewelsCollected >= state.JewelQuota:
                state.IsCaveEnded = true;
                state.AdvanceToNextCave = true;
                state.EntranceProgress = 0;

                // Rockford zieht nur in die Tür — der Ausgang ist KEIN Diamant. Das DOS-Original
                // sprang hier auf den Diamant-Zweig durch und wertete das Betreten des Ausgangs als
                // eingesammelten Diamanten (Zähler, Punkte und Sammel-Sound inklusive).
                MoveTo(targetIndex);
                break;

            case JewelObject:
                state.JewelsCollected++;
                if (state.JewelsCollected >= state.JewelQuota)
                {
                    state.CurrentJewelPoints = state.PointsPerJewelAfterQuota;
                }

                state.Score += state.CurrentJewelPoints;
                state.SoundEvents.Enqueue(SoundEvent.CollectJewel);
                MoveTo(targetIndex);
                break;

            case EarthObject:
                state.SoundEvents.Enqueue(SoundEvent.WalkEarth);
                MoveTo(targetIndex);
                break;

            case EmptyObject:
                state.SoundEvents.Enqueue(SoundEvent.WalkEmpty);
                MoveTo(targetIndex);
                break;

            case BoulderObject boulder:
                Push(boulder, targetIndex);
                break;
        }
    }

    /// <summary>
    /// Die vier Scroll-Auslöser (:893-896). Abweichung vom Original: Die Schwellen liegen je eine
    /// Kachel weiter innen, damit das Scrollen einen Schritt früher einsetzt. Schwellen und
    /// Scrollweiten leiten sich aus der Sichtfenstergröße ab (siehe ViewportSize) und sind beim
    /// Original-Sichtfenster 20x12 identisch mit den dortigen Konstanten (16/8/7/5); zeigt das
    /// Sichtfenster die ganze Cave, greifen die Wächter und es wird gar nicht gescrollt.
    /// </summary>
    private void ScrollCamera()
    {
        var camera = Cave.Camera;
        var viewport = camera.Viewport;

        var col = Index % Cave.Width;
        var row = Index / Cave.Width;

        if (camera.X + viewport.ScrollTriggerRight < col && camera.X < Cave.Width - viewport.Columns)
        {
            camera.Relx = (sbyte)viewport.ScrollAmountX;
        }

        if (camera.X + ViewportSize.ScrollTriggerNear == col && camera.X > 0)
        {
            camera.Relx = (sbyte)-viewport.ScrollAmountX;
        }

        if (camera.Y + viewport.ScrollTriggerBottom < row && camera.Y < Cave.Height - viewport.Rows)
        {
            camera.Rely = (sbyte)viewport.ScrollAmountY;
        }

        if (camera.Y + ViewportSize.ScrollTriggerNear == row && camera.Y > 0)
        {
            camera.Rely = (sbyte)-viewport.ScrollAmountY;
        }
    }

    /// <summary>
    /// Schieben nach BD1 (BDCFF 0006): nur waagerecht, nur RUHENDE Steine ("he cannot push falling
    /// boulders"), und dann mit einer Chance von 1:8 pro Versuch. Der Wurf steht bewusst hinter den
    /// geometrischen Prüfungen — gewürfelt wird nur, wenn Rockford es tatsächlich versucht ("each
    /// frame that he tries").
    /// Das DOS-Original nutzte stattdessen ein festes Clk4-Fenster (jeder 2. Scan) und ließ auch
    /// fallende Steine schieben.
    /// </summary>
    private void Push(BoulderObject boulder, int targetIndex)
    {
        var direction = Cave.Input.Direction;
        var behindIndex = Index + (direction * 2);

        var horizontal = direction is 1 or -1;
        if (!horizontal || boulder.Falling || !Cave.Get(behindIndex).IsFreeSpace || Cave.Random.Next(8) != 0)
        {
            return;
        }

        boulder.ScannedThisFrame = true;
        Cave.Set(behindIndex, boulder);

        MoveTo(targetIndex);
        Cave.State.SoundEvents.Enqueue(SoundEvent.PushBoulder);
    }

    /// <summary>
    /// Zieht um — oder greift nur hinein. Beim Greifen räumt Rockford die Zielzelle leer und bleibt
    /// selbst stehen; das Original erledigt beides mit demselben Code, indem es die Kachelbytes mit
    /// dem Greif-Modifikator XOR-verknüpft (0x86 ^ 6 == 0x80).
    /// </summary>
    private void MoveTo(int targetIndex)
    {
        var from = Index;

        ScannedThisFrame = true;

        if (Cave.Input.IsGrabbing)
        {
            Cave.Spawn(targetIndex, new EmptyObject(Cave) { ScannedThisFrame = true });
            return;
        }

        Cave.Set(targetIndex, this);
        Cave.Spawn(from, new EmptyObject(Cave) { ScannedThisFrame = true });
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
