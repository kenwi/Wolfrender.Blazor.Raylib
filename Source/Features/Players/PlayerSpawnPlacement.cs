namespace Game.Features.Players;

/// <summary>Player spawn position and facing stored in level data.</summary>
public class PlayerSpawnPlacement
{
    public int TileX { get; set; } = 30;
    public int TileY { get; set; } = 28;
    public float WorldY { get; set; } = 2f;
    /// <summary>Facing in radians, snapped to 45° steps (same convention as <see cref="EnemyPlacement.Rotation"/>).</summary>
    public float Rotation { get; set; } = -MathF.PI / 2f;
}
