namespace BoulderDash.Core.Data;

/// <summary>
/// Parser für LEVEL.BIN. Jeder Datensatz belegt exakt 898 Bytes auf der Platte
/// (16-Byte-Header + 880-Byte-Kachelblock + 2 reservierte Füllbytes), unabhängig von der
/// tatsächlich im Header angegebenen Cave-Größe (siehe level_laden, src/BOULDER.CPP:975-1040
/// bzw. die feste Fread-Grenze 40*22+16 sowie den Datensatzabstand 40*22+18).
/// </summary>
public static class CaveFile
{
    private const int HeaderSize = 16;
    private const int MaxTileBlockSize = 40 * 22;
    private const int RecordStride = HeaderSize + MaxTileBlockSize + 2;

    public static IReadOnlyList<CaveData> LoadAll(string path)
    {
        var bytes = File.ReadAllBytes(path);

        // Der letzte Datensatz im Original-LEVEL.BIN hat keine abschließenden 2 Füllbytes
        // mehr (die reale Datei ist exakt 2 Bytes kürzer als 21 volle 898-Byte-Datensätze:
        // 18856 = 20*898 + 896). Der letzte Datensatz braucht daher keinen Platz mehr für
        // sein eigenes Trennpolster.
        var nutzbareBytes = bytes.Length - HeaderSize - MaxTileBlockSize;
        var count = nutzbareBytes < 0 ? 0 : nutzbareBytes / RecordStride + 1;
        var result = new List<CaveData>(count);

        for (var i = 0; i < count; i++)
        {
            result.Add(ParseRecord(bytes, i));
        }

        return result;
    }

    private static CaveData ParseRecord(byte[] bytes, int index)
    {
        var offset = index * RecordStride;

        // Header-Reihenfolge exakt wie level_laden, BOULDER.CPP:1014-1027.
        var width = bytes[offset + 0];
        var height = bytes[offset + 1];
        var jewelQuota = bytes[offset + 2];
        var timeSeconds = bytes[offset + 3];
        var baseColors = new[] { bytes[offset + 4], bytes[offset + 5], bytes[offset + 6], bytes[offset + 7] };
        var cameraStartX = bytes[offset + 8];
        var cameraStartY = bytes[offset + 9];
        var enchantedWallSeconds = bytes[offset + 10];
        var pointsBeforeQuota = bytes[offset + 11];
        var pointsAfterQuota = bytes[offset + 12];
        var gameSpeed = bytes[offset + 13];
        // Bytes 14-15: reserviert, ungenutzt.

        var tileBlockOffset = offset + HeaderSize;
        var tileCount = width * height;
        var tiles = new byte[tileCount];
        Array.Copy(bytes, tileBlockOffset, tiles, 0, tileCount);

        return new CaveData
        {
            Index = index,
            Width = width,
            Height = height,
            JewelQuota = jewelQuota,
            TimeSeconds = timeSeconds,
            BaseColors = baseColors,
            CameraStartX = cameraStartX,
            CameraStartY = cameraStartY,
            EnchantedWallSeconds = enchantedWallSeconds,
            PointsPerJewelBeforeQuota = pointsBeforeQuota,
            PointsPerJewelAfterQuota = pointsAfterQuota,
            GameSpeed = gameSpeed,
            Tiles = tiles,
        };
    }
}
