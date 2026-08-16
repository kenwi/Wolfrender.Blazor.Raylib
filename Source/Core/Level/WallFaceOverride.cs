namespace Game.Core.Level;

/// <summary>
/// Per-face texture exceptions for one wall cell. Null or 0 on a face means use <see cref="MapData.Walls"/>.
/// </summary>
public class WallFaceOverride
{
    public int TileX { get; set; }
    public int TileY { get; set; }
    public uint? North { get; set; }
    public uint? East { get; set; }
    public uint? South { get; set; }
    public uint? West { get; set; }

    public bool HasAnyFace => IsSet(North) || IsSet(East) || IsSet(South) || IsSet(West);

    public uint? Get(WallFace face) => face switch
    {
        WallFace.North => North,
        WallFace.East => East,
        WallFace.South => South,
        WallFace.West => West,
        _ => null
    };

    public void Set(WallFace face, uint? tileId)
    {
        uint? value = Normalize(tileId);
        switch (face)
        {
            case WallFace.North:
                North = value;
                break;
            case WallFace.East:
                East = value;
                break;
            case WallFace.South:
                South = value;
                break;
            case WallFace.West:
                West = value;
                break;
        }
    }

    public WallFaceOverride Clone() => new()
    {
        TileX = TileX,
        TileY = TileY,
        North = North,
        East = East,
        South = South,
        West = West
    };

    public static bool ContentEquals(WallFaceOverride? a, WallFaceOverride? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.TileX == b.TileX
            && a.TileY == b.TileY
            && a.North == b.North
            && a.East == b.East
            && a.South == b.South
            && a.West == b.West;
    }

    public static bool IsSet(uint? tileId) => tileId is > 0;

    private static uint? Normalize(uint? tileId) => tileId is > 0 ? tileId : null;
}
