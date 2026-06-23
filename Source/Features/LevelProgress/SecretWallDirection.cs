namespace Game.Features.LevelProgress;

/// <summary>Cardinal direction a secret wall slides when activated.</summary>
public enum SecretWallDirection
{
    North,
    East,
    South,
    West
}

public static class SecretWallDirectionHelper
{
    public static (int Dx, int Dy) ToTileDelta(SecretWallDirection direction) => direction switch
    {
        SecretWallDirection.North => (0, -1),
        SecretWallDirection.East => (1, 0),
        SecretWallDirection.South => (0, 1),
        SecretWallDirection.West => (-1, 0),
        _ => (0, 0)
    };
}
