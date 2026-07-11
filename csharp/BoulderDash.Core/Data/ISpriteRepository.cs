namespace BoulderDash.Core.Data;

/// <summary>Quelle der Sprites, unabhängig vom Speicherformat. Get() liefert die Objekte in fester
/// Reihenfolge; ihre Frames hintereinander gereiht ergeben die 49 Rohsprites, deren Index
/// SpriteTables.FrameToRawSprite referenziert (ursprünglich die Reihenfolge in SPRITES.BIN,
/// Load_Sprites in src/INTRO.CPP:133-151).</summary>
public interface ISpriteRepository
{
    /// <summary>Alle Sprite-Objekte (ein Eintrag je Objekt, mit seinen Animationsframes) in
    /// Rohsprite-Reihenfolge.</summary>
    IEnumerable<SpriteData> Get();
}
