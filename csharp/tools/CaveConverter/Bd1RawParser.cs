using BoulderDash.Core.Data;

namespace CaveConverter;

/// <summary>
/// Dekodiert einen rohen BD1-Cave-Datensatz (32-Byte-Header + $FF-terminierte Zeichenbefehle,
/// wie in caves.json unter "hex") in 5 CaveDefinition (Level 1-5). Portiert 1:1 aus decode_cave /
/// dem Header-Layout in Boulder-Dash-C64/disassembly/tools/extract_data.py.
/// </summary>
public static class Bd1RawParser
{
    public static CaveDefinition[] ParseLevels(byte[] data, Cave cave, string name, string description, bool isIntermission)
    {
        var magicWallTime = data[1];
        var jewelValue = data[2];
        var jewelValueExtra = data[3];
        var seeds = data[4..9];
        var jewelsNeeded = data[9..14];
        var times = data[14..19];
        var colors = data[0x13..0x16];
        var randObj = data[0x18..0x1C];
        var randProb = data[0x1C..0x20];

        var randomFill = new List<RandomFillEntry>();
        for (var i = 0; i < 4; i++)
        {
            if (randProb[i] > 0)
            {
                randomFill.Add(new RandomFillEntry(Bd1ObjectCode.ToElement(randObj[i]), randProb[i]));
            }
        }

        var drawCommands = ParseDrawCommands(data);

        var levels = new CaveDefinition[5];
        for (var level = 0; level < 5; level++)
        {
            levels[level] = new CaveDefinition
            {
                Cave = cave,
                Level = (CaveLevel)(level + 1),
                Name = name,
                Description = description,
                IsIntermission = isIntermission,
                Width = 40,
                Height = 22,
                RandomSeed = seeds[level],
                JewelsNeeded = jewelsNeeded[level],
                CaveTime = times[level],
                MagicWallTime = magicWallTime,
                JewelValue = jewelValue,
                JewelValueExtra = jewelValueExtra,
                Colors = colors,
                RandomFill = randomFill,
                DrawCommands = drawCommands,
            };
        }

        return levels;
    }

    private static List<DrawCommand> ParseDrawCommands(byte[] data)
    {
        var commands = new List<DrawCommand>();
        var i = 0x20;
        while (i < data.Length && data[i] != 0xFF)
        {
            var code = data[i];
            var obj = Bd1ObjectCode.ToElement((byte)(code & 0x3F));
            var kind = code >> 6;
            switch (kind)
            {
                case 0: // einzelnes Objekt
                    commands.Add(new DrawCommand(DrawKind.Point, obj, data[i + 1], data[i + 2] - 2));
                    i += 3;
                    break;
                case 1: // Linie
                    commands.Add(new DrawCommand(
                        DrawKind.Line, obj, data[i + 1], data[i + 2] - 2,
                        Length: data[i + 3], Direction: (Bd1Direction)(data[i + 4] & 7)));
                    i += 5;
                    break;
                case 2: // gefülltes Rechteck
                    commands.Add(new DrawCommand(
                        DrawKind.FilledRect, obj, data[i + 1], data[i + 2] - 2,
                        Width: data[i + 3], Height: data[i + 4], FillElement: Bd1ObjectCode.ToElement(data[i + 5])));
                    i += 6;
                    break;
                default: // Rechteck (nur Rand)
                    commands.Add(new DrawCommand(
                        DrawKind.Rect, obj, data[i + 1], data[i + 2] - 2,
                        Width: data[i + 3], Height: data[i + 4]));
                    i += 5;
                    break;
            }
        }

        return commands;
    }
}
