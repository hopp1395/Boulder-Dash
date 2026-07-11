using System.Text;

namespace BoulderDash.Core.Data;

/// <summary>Schreibt eine Cave im menschenlesbaren Textformat (Gegenstück zu CaveTextFile.Parse):
/// Kopfdaten plus die Kachelkarte als ASCII-Raster im [Map]-Abschnitt. Die Karte ist dabei echter
/// Dateiinhalt, keine Vorschau — sie wird beim Laden genau so übernommen.</summary>
public static class CaveTextWriter
{
    public static string Write(CaveDefinition def, string[] mapLines)
    {
        var sb = new StringBuilder();
        var letter = def.Letter;
        var kind = def.IsIntermission ? "Intermission" : "Normal";

        sb.AppendLine($"# Boulder Dash — Cave {letter}, Level {def.Level}");
        sb.AppendLine("# Erzeugt aus den BD1-Rohdaten (Boulder-Dash-C64/extracted/caves).");
        sb.AppendLine();

        sb.AppendLine("[Cave]");
        sb.AppendLine($"Cave        = {letter}");
        sb.AppendLine($"Name        = {def.Name}");
        sb.AppendLine($"Description = {def.Description}");
        sb.AppendLine($"Kind        = {kind}");
        sb.AppendLine($"Level       = {def.Level}");
        sb.AppendLine($"Width       = {def.Width}");
        sb.AppendLine($"Height      = {def.Height}");
        sb.AppendLine();

        sb.AppendLine("[Rules]");
        sb.AppendLine($"JewelsNeeded    = {def.JewelsNeeded}");
        sb.AppendLine($"CaveTime        = {def.CaveTime}");
        sb.AppendLine($"MagicWallTime   = {def.MagicWallTime}");
        sb.AppendLine($"JewelValue      = {def.JewelValue}");
        sb.AppendLine($"JewelValueExtra = {def.JewelValueExtra}");
        sb.AppendLine($"Colors          = {def.Colors[0]}, {def.Colors[1]}, {def.Colors[2]}");
        sb.AppendLine();

        sb.AppendLine("[Map]");
        sb.AppendLine("# Die Karte ist die maßgebliche Kachelquelle - Änderungen hier wirken direkt im Spiel.");
        sb.AppendLine("# Legende: W Steel, w Wall, M MagicWall, . Dirt, ' ' Space, r Boulder, d Jewel,");
        sb.AppendLine("#          P Inbox, X Outbox, F Firefly, B Butterfly, a Amoeba");
        foreach (var mapLine in mapLines)
        {
            sb.AppendLine(mapLine);
        }

        return sb.ToString();
    }
}
