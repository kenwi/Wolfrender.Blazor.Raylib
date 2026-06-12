namespace Game.Features.Players;

/// <summary>JSON DTO for <see cref="PlayerSpawnPlacement"/>. Owns the mapping for this slice.</summary>
public class PlayerSpawnData
{
    public int TileX { get; set; } = 30;
    public int TileY { get; set; } = 28;
    public float WorldY { get; set; } = 2f;
    public float Rotation { get; set; } = -MathF.PI / 2f;

    public static PlayerSpawnData FromPlacement(PlayerSpawnPlacement spawn) => new()
    {
        TileX = spawn.TileX,
        TileY = spawn.TileY,
        WorldY = spawn.WorldY,
        Rotation = spawn.Rotation
    };

    /// <summary>Copies the DTO values onto the level's existing spawn placement.</summary>
    public void ApplyTo(PlayerSpawnPlacement spawn)
    {
        spawn.TileX = TileX;
        spawn.TileY = TileY;
        spawn.WorldY = WorldY;
        spawn.Rotation = Rotation;
    }
}
