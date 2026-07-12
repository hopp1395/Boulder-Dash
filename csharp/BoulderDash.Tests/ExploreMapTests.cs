using BoulderDash.Core.Simulation;

namespace BoulderDash.Tests;

/// <summary>
/// Die Landkarte des Cave-Explore (siehe ExploreMap): Was Rockford einmal gesehen hat, bleibt
/// erkundet — über den Tod hinaus. Ein verlorenes Leben kostet ein Leben, nicht die Landkarte; erst
/// ein neues Spiel fängt wieder im Dunkeln an.
///
/// Der Schlüssel dafür ist der Cave-Name: Nur an ihm erkennt die Karte beim Laden, ob es dieselbe
/// Cave ist wie eben (Neustart nach dem Tod) oder die nächste in der Kette.
/// </summary>
public class ExploreMapTests
{
    private const int Width = 40;
    private const int Height = 22;

    /// <summary>Eine Kachel weit weg vom Eingang — außerhalb des Blickradius, der beim Cave-Start um
    /// den Eingang liegt.</summary>
    private const int FarX = 35;
    private const int FarY = 18;

    private const int EntranceIndex = (2 * Width) + 2;

    private static ExploreMap NewMap(string caveName)
    {
        var map = new ExploreMap { Enabled = true };
        map.BeginCave(caveName, Width, Height, EntranceIndex);
        return map;
    }

    [Fact]
    public void Dieselbe_Cave_erneut_geladen_behaelt_das_Erkundete()
    {
        var map = NewMap("cave-A-1");
        Assert.Equal(TileVisibility.Hidden, map.Visibility(FarX, FarY));

        map.Reveal((FarY * Width) + FarX); // dort ist Rockford gewesen

        // Tod: dieselbe Cave noch einmal. Der Blick liegt wieder auf dem Eingang, die Kachel ist
        // deshalb nicht mehr im Blickradius — aber sie bleibt erkundet.
        map.BeginCave("cave-A-1", Width, Height, EntranceIndex);

        Assert.Equal(TileVisibility.Explored, map.Visibility(FarX, FarY));
    }

    [Fact]
    public void Eine_andere_Cave_faengt_im_Dunkeln_an()
    {
        var map = NewMap("cave-A-1");
        map.Reveal((FarY * Width) + FarX);

        map.BeginCave("cave-B-1", Width, Height, EntranceIndex);

        Assert.Equal(TileVisibility.Hidden, map.Visibility(FarX, FarY));
    }

    [Fact]
    public void Neues_Spiel_vergisst_alles()
    {
        var map = NewMap("cave-A-1");
        map.Reveal((FarY * Width) + FarX);

        map.Reset();
        map.BeginCave("cave-A-1", Width, Height, EntranceIndex);

        Assert.Equal(TileVisibility.Hidden, map.Visibility(FarX, FarY));
    }

    /// <summary>Der Eingang ist beim Cave-Start immer erkundet — auch beim Neustart nach dem Tod, denn
    /// Rockford platzt dort heraus und darf nicht im Schwarzen stehen.</summary>
    [Fact]
    public void Der_Eingang_ist_immer_erkundet()
    {
        var map = NewMap("cave-A-1");
        map.Reveal((FarY * Width) + FarX);
        map.BeginCave("cave-A-1", Width, Height, EntranceIndex);

        Assert.Equal(TileVisibility.Visible, map.Visibility(2, 2));
    }
}
