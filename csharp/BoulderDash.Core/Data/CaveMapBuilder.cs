using BoulderDash.Core.Simulation;

namespace BoulderDash.Core.Data;

/// <summary>
/// Baut aus einer CaveDefinition (BD1-Rohformat) die Kachelkarte auf: Zufallsfüllung per Bd1Random,
/// dann die Zeichenbefehle, dann der abschließende Stahlrahmen. Portiert 1:1 aus decode_cave in
/// Boulder-Dash-C64/disassembly/tools/extract_data.py.
///
/// Wird zur LAUFZEIT nicht mehr benutzt: die Cave-Textdateien enthalten die fertige Kachelkarte
/// ([Map]-Abschnitt, siehe CaveTextFile). Nur noch tools/CaveConverter nutzt diesen Aufbau, um die
/// Textdateien einmalig aus den BD1-Rohdaten zu erzeugen.
/// </summary>
public static class CaveMapBuilder
{
    public static CaveData Build(CaveDefinition def)
    {
        var width = def.Width;
        var height = def.Height;
        var tiles = new byte[width * height];
        Array.Fill(tiles, (byte)Element.TitaniumWall);

        var random = new Bd1Random(def.RandomSeed);
        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = random.Next();
                var element = Element.Earth;
                foreach (var entry in def.RandomFill)
                {
                    if (value < entry.Probability)
                    {
                        element = entry.Element;
                    }
                }

                tiles[(y * width) + x] = (byte)element;
            }
        }

        void Store(int x, int y, Element element)
        {
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                tiles[(y * width) + x] = (byte)element;
            }
        }

        foreach (var cmd in def.DrawCommands)
        {
            switch (cmd.Kind)
            {
                case DrawKind.Point:
                    Store(cmd.X, cmd.Y, cmd.Element);
                    break;
                case DrawKind.Line:
                    var (dx, dy) = Bd1DirectionOffsets.Of(cmd.Direction);
                    for (var k = 0; k < cmd.Length; k++)
                    {
                        Store(cmd.X + (k * dx), cmd.Y + (k * dy), cmd.Element);
                    }

                    break;
                case DrawKind.FilledRect:
                    for (var yy = 0; yy < cmd.Height; yy++)
                    {
                        for (var xx = 0; xx < cmd.Width; xx++)
                        {
                            var edge = yy == 0 || yy == cmd.Height - 1 || xx == 0 || xx == cmd.Width - 1;
                            Store(cmd.X + xx, cmd.Y + yy, edge ? cmd.Element : cmd.FillElement);
                        }
                    }

                    break;
                case DrawKind.Rect:
                    for (var yy = 0; yy < cmd.Height; yy++)
                    {
                        for (var xx = 0; xx < cmd.Width; xx++)
                        {
                            if (yy == 0 || yy == cmd.Height - 1 || xx == 0 || xx == cmd.Width - 1)
                            {
                                Store(cmd.X + xx, cmd.Y + yy, cmd.Element);
                            }
                        }
                    }

                    break;
            }
        }

        // Abschließender Stahlrahmen (wie im Original nach den Zeichenbefehlen aufgetragen).
        for (var x = 0; x < width; x++)
        {
            tiles[x] = (byte)Element.TitaniumWall;
            tiles[((height - 1) * width) + x] = (byte)Element.TitaniumWall;
        }

        for (var y = 0; y < height; y++)
        {
            tiles[y * width] = (byte)Element.TitaniumWall;
            tiles[(y * width) + width - 1] = (byte)Element.TitaniumWall;
        }

        var entranceIndex = Array.IndexOf(tiles, (byte)Element.Entrance);
        var entranceX = entranceIndex < 0 ? 0 : entranceIndex % width;
        var entranceY = entranceIndex < 0 ? 0 : entranceIndex / width;
        var cameraStartX = Math.Clamp(entranceX - 10, 0, Math.Max(0, width - 20));
        var cameraStartY = Math.Clamp(entranceY - 6, 0, Math.Max(0, height - 12));

        return new CaveData
        {
            Index = def.Letter - 'A',
            Name = def.Name,
            Description = def.Description,
            Letter = def.Letter,
            IsIntermission = def.IsIntermission,
            Width = width,
            Height = height,
            JewelQuota = def.JewelsNeeded,
            TimeSeconds = def.CaveTime,
            // Konvention des ursprünglichen 1999er-Ports (empirisch aus LEVEL.BIN übernommen):
            // BaseColors=[0,1,Farbe2,Farbe1] - die dritte BD1-Rohfarbe bleibt ungenutzt.
            BaseColors = [0, 1, def.Colors[1], def.Colors[0]],
            CameraStartX = (byte)cameraStartX,
            CameraStartY = (byte)cameraStartY,
            EnchantedWallSeconds = def.MagicWallTime,
            PointsPerJewelBeforeQuota = def.JewelValue,
            PointsPerJewelAfterQuota = def.JewelValueExtra,
            GameSpeed = 1,
            Tiles = tiles,
        };
    }
}
