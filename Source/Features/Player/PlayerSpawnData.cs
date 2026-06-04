namespace Game.Features.Players;

/// <summary>JSON DTO for <see cref="PlayerSpawnPlacement"/>.</summary>
public class PlayerSpawnData
{
    public int TileX { get; set; } = 30;
    public int TileY { get; set; } = 28;
    public float WorldY { get; set; } = 2f;
    public float Rotation { get; set; } = -MathF.PI / 2f;
}
