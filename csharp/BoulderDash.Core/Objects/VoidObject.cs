using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Objects;

/// <summary>
/// Das Nichts außerhalb der Höhle: die Kacheln des Gitters, die nicht zur Cave gehören.
///
/// Eine Cave muss weder rechteckig sein noch eine bestimmte Größe haben — sie ist eine beliebig
/// geformte Höhle irgendwo im Gitter, und alles, was um sie herum übrig bleibt, ist Void (siehe
/// CaveTextFile). Gezeichnet wird dort nichts (Frame 0, schwarz wie der Leerraum), und die
/// Bildschirm-Verdeckung lässt es aus — sonst stünde beim Auf- und Zudecken ein Stahl-RECHTECK da
/// statt der Silhouette der Höhle (<see cref="CaveObject.CoveredByScreen"/>).
///
/// Eigene Spielregeln hat es keine: Es ERBT den Leerraum und ist für die Physik dasselbe Nichts.
/// Hineinlaufen oder -fallen kann trotzdem niemand, denn jede Cave ist lückenlos von Stahl
/// umschlossen — darauf besteht der Parser (CaveTextFile.ValidateEnclosure).
///
/// Keine Original-Entsprechung: Im DOS-Original wie in BD1 war jede Cave ein Rechteck mit
/// Stahl-Randmauer, und außerhalb gab es schlicht nichts, was hätte dargestellt werden können.
/// </summary>
public sealed class VoidObject : EmptyObject
{
    public VoidObject(Cave cave)
        : base(cave)
    {
    }

    public override Element Element => Element.Void;

    public override char MapGlyph => '_';

    /// <summary>Außerhalb der Höhle hat der Rand-Füllstein der Verdeckung nichts verloren — dort
    /// bleibt es schwarz, auch während des Auf- und Zudeckens.</summary>
    public override bool CoveredByScreen => false;

    /// <summary>
    /// Die einzige Stelle, an der das Nichts NICHT der Leerraum ist, von dem es erbt: Es hält wie
    /// Stahl. Zwar kommt keine Explosion durch die Stahlmauer, wohl aber ÜBER ihre Ecke: Der 3x3-Schlag
    /// (CaveObject.Explode) erfasst auch die Diagonale, und dort kann bei einer schräg verlaufenden
    /// Mauer schon das Nichts liegen. Als Leerraum bliebe davon eine Empty-Kachel außerhalb der Cave
    /// zurück — sichtbar erst am Cave-Ende, wenn die Verdeckung sie als einzige Kachel im Nichts
    /// wieder zustahlt (<see cref="CoveredByScreen"/>).
    /// </summary>
    public override void Detonate(Func<ExplosionObject> create)
    {
    }
}
