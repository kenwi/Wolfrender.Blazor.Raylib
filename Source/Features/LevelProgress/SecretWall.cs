using System.Numerics;

namespace Game.Features.LevelProgress;

/// <summary>Runtime push-wall instance built from <see cref="SecretWallPlacement"/>.</summary>
public sealed class SecretWall
{
    public int TileX { get; init; }
    public int TileY { get; init; }
    public Vector2 StartPosition { get; set; }
    public Vector2 Position { get; set; }
    public SecretWallDirection Direction { get; init; }
    public int TravelTiles { get; init; }
    public uint WallTileId { get; init; }
    public SecretWallState State { get; set; } = SecretWallState.Idle;
    public bool HasScored { get; set; }

    public Vector2 EndPosition
    {
        get
        {
            var (dx, dy) = SecretWallDirectionHelper.ToTileDelta(Direction);
            return StartPosition + new Vector2(dx, dy) * TravelTiles;
        }
    }
}
