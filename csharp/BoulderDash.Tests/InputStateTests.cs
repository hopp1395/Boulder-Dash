using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

/// <summary>
/// InputState hält nach BD1 alle gedrückten Richtungen gleichzeitig; bei diagonaler Eingabe gilt
/// "horizontal movement takes precedence over the vertical" (BDCFF 0006). Das DOS-Original merkte
/// sich stattdessen nur die zuletzt gedrückte Richtung.
/// </summary>
public class InputStateTests
{
    private const int CaveWidth = 40;

    [Fact]
    public void Waagerecht_schlaegt_senkrecht_bei_diagonaler_Eingabe()
    {
        var input = new InputState();

        input.PressDown(CaveWidth);
        Assert.Equal(CaveWidth, input.Direction);

        input.PressRight(); // diagonal: rechts + runter
        Assert.Equal(1, input.Direction);
    }

    /// <summary>Der Kern des behobenen DOS-Quirks: Wird die zweite Taste wieder losgelassen, läuft
    /// Rockford in der noch gehaltenen ersten Richtung weiter. Früher überschrieb jedes Press den
    /// Zustand komplett — er blieb dann einfach stehen.</summary>
    [Fact]
    public void Loslassen_der_zweiten_Taste_setzt_die_noch_gehaltene_erste_fort()
    {
        var input = new InputState();

        input.PressRight();
        input.PressDown(CaveWidth);
        input.ReleaseRight(); // rechts los, runter bleibt gehalten

        Assert.Equal(CaveWidth, input.Direction);
    }

    [Fact]
    public void Innerhalb_einer_Achse_gewinnt_die_zuletzt_gedrueckte_Taste()
    {
        var input = new InputState();

        input.PressLeft();
        input.PressRight(); // beide waagerecht gehalten
        Assert.Equal(1, input.Direction);

        input.ReleaseRight(); // links ist noch gehalten
        Assert.Equal(-1, input.Direction);
    }

    [Fact]
    public void Ohne_gehaltene_Taste_steht_Rockford()
    {
        var input = new InputState();

        input.PressUp(CaveWidth);
        input.ReleaseUp();

        Assert.Equal(0, input.Direction);
    }

    [Fact]
    public void ResetForNewCave_loest_alle_Richtungen_und_das_Greifen()
    {
        var input = new InputState();
        input.PressRight();
        input.PressGrab();

        input.ResetForNewCave();

        Assert.Equal(0, input.Direction);
        Assert.Equal((byte)0, input.GrabModifier);
        Assert.Equal((byte)0, input.FacingLeft); // Blickrichtung bleibt erhalten (Original)
    }
}
