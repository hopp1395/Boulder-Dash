using BoulderDash.Core.Data;
using BoulderDash.Core.Flow;

namespace BoulderDash.Tests;

public class GameSessionTests
{
    private static readonly string LevelBinPath = Path.Combine(TestPaths.GameAssets, "LEVEL.BIN");

    private static GameSession NewRealSession() => new(CaveFile.LoadAll(LevelBinPath));

    [Fact]
    public void Menu_Cave_Auswahl_ist_auf_0_bis_15_begrenzt()
    {
        var session = NewRealSession();

        for (var i = 0; i < 30; i++)
        {
            session.MenuNextCave();
        }

        Assert.Equal(15, session.CaveIndex);

        for (var i = 0; i < 30; i++)
        {
            session.MenuPreviousCave();
        }

        Assert.Equal(0, session.CaveIndex);
    }

    [Fact]
    public void Schwierigkeitsgrad_ist_auf_1_bis_4_begrenzt()
    {
        var session = NewRealSession();

        for (var i = 0; i < 10; i++)
        {
            session.MenuUp();
        }

        Assert.Equal(4, session.DifficultyLevel);

        for (var i = 0; i < 10; i++)
        {
            session.MenuDown();
        }

        Assert.Equal(1, session.DifficultyLevel);
    }

    [Fact]
    public void F1_startet_Session_mit_3_Chancen_und_geht_in_Playing()
    {
        var session = NewRealSession();
        session.State.Chances = 1; // simuliert Rest einer vorherigen (verlorenen) Session

        session.MenuStart();

        Assert.Equal(SessionPhase.Playing, session.Phase);
        Assert.Equal(3, session.State.Chances);
        Assert.NotNull(session.Cave);
        Assert.Equal('A', session.CurrentCaveData!.Letter);
    }

    [Fact]
    public void Escape_waehrend_des_Spiels_fuehrt_nach_der_Uebergangspause_zurueck_ins_Menue()
    {
        var session = NewRealSession();
        session.MenuStart();

        session.EscapePressed();
        Assert.Equal(SessionPhase.CaveTransition, session.Phase);

        session.Update(10.0); // Übergangspause (0,5s) sicher überschreiten

        Assert.Equal(SessionPhase.Menu, session.Phase);
        Assert.Null(session.Cave);
    }

    [Fact]
    public void Erfolgreicher_Ausgang_fuehrt_ueber_Bonuszaehlung_und_Uebergang_zur_naechsten_Cave()
    {
        var session = NewRealSession();
        session.MenuStart();

        // Simuliert einen erfolgreichen Ausgang, wie ihn CavePhysics.ProcessRockford setzt.
        session.State.IsCaveEnded = true;
        session.State.Stat = 0;
        session.State.AdvanceToNextCave = true;
        session.State.CaveTimeRemaining = 5;

        session.Update(0.001); // erkennt IsCaveEnded -> LevelEndBonus
        Assert.Equal(SessionPhase.LevelEndBonus, session.Phase);

        session.Update(10.0); // Bonuszählung + Nachpause sicher abschließen
        Assert.Equal(SessionPhase.CaveTransition, session.Phase);

        session.Update(10.0); // Übergangspause abschließen -> nächste Cave (B)
        Assert.Equal(SessionPhase.Playing, session.Phase);
        Assert.Equal('B', session.CurrentCaveData!.Letter);
    }

    [Fact]
    public void Tod_kostet_ein_Leben_und_laedt_dieselbe_Cave_erneut()
    {
        var session = NewRealSession();
        session.MenuStart();
        var chancesVorher = session.State.Chances;

        session.State.IsCaveEnded = true;
        session.State.Stat = 1; // Rockford nicht mehr gefunden -> tot

        session.Update(0.001);
        Assert.Equal(SessionPhase.DeathPause, session.Phase);
        Assert.Equal(chancesVorher - 1, session.State.Chances);

        session.Update(10.0); // delay(1000) überschreiten
        session.AnyKeyPressed();
        Assert.Equal(SessionPhase.CaveTransition, session.Phase);

        session.Update(10.0);
        Assert.Equal(SessionPhase.Playing, session.Phase);
        Assert.Equal('A', session.CurrentCaveData!.Letter); // dieselbe Cave, kein Fortschritt
    }

    [Fact]
    public void Game_Over_bei_0_Chancen_fuehrt_zurueck_ins_Menue()
    {
        var session = NewRealSession();
        session.MenuStart();
        session.State.Chances = 1; // letztes Leben

        session.State.IsCaveEnded = true;
        session.State.Stat = 1;

        session.Update(0.001);
        Assert.Equal(0, session.State.Chances);
        Assert.Equal(SessionPhase.DeathPause, session.Phase);

        session.Update(10.0); // erste Pause -> GameOverMessage
        Assert.Equal(SessionPhase.GameOverMessage, session.Phase);
        Assert.True(session.ShowGameOverMessage);

        session.Update(10.0); // zweite Pause
        session.AnyKeyPressed();
        Assert.Equal(SessionPhase.CaveTransition, session.Phase);

        session.Update(10.0);
        Assert.Equal(SessionPhase.Menu, session.Phase);
    }

    [Fact]
    public void Leere_Platzhalter_Cave_wird_beim_Laden_uebersprungen()
    {
        byte[] blankTiles = new byte[40 * 22]; // alles 0, kein Eingang
        byte[] validTiles = new byte[20 * 12];
        // Minimaler gültiger 20x12-Rand mit Eingang und Ausgang.
        for (var x = 0; x < 20; x++)
        {
            validTiles[x] = 5;
            validTiles[(11 * 20) + x] = 5;
        }

        for (var y = 0; y < 12; y++)
        {
            validTiles[y * 20] = 5;
            validTiles[(y * 20) + 19] = 5;
        }

        validTiles[(1 * 20) + 1] = 10; // Eingang
        validTiles[(1 * 20) + 2] = 11; // Ausgang

        var blank = new CaveData
        {
            Index = 0, Width = 40, Height = 22, JewelQuota = 0, TimeSeconds = 99,
            BaseColors = [0, 1, 2, 3], CameraStartX = 0, CameraStartY = 0,
            EnchantedWallSeconds = 0, PointsPerJewelBeforeQuota = 10, PointsPerJewelAfterQuota = 20,
            GameSpeed = 1, Tiles = blankTiles,
        };
        var valid = new CaveData
        {
            Index = 1, Width = 20, Height = 12, JewelQuota = 0, TimeSeconds = 99,
            BaseColors = [0, 1, 2, 3], CameraStartX = 0, CameraStartY = 0,
            EnchantedWallSeconds = 0, PointsPerJewelBeforeQuota = 10, PointsPerJewelAfterQuota = 20,
            GameSpeed = 1, Tiles = validTiles,
        };

        var session = new GameSession([blank, valid]);
        session.MenuStart(); // CaveIndex steht auf 0 (blank)

        Assert.Equal(SessionPhase.Playing, session.Phase);
        Assert.Equal(1, session.CaveIndex); // auf die gültige Cave übergesprungen
    }
}
