namespace Game.Features.Enemies;

/// <summary>
/// Describes a placed enemy in tile coordinates, used for editor and serialization.
/// </summary>
public class EnemyPlacement
{
    public int TileX { get; set; }
    public int TileY { get; set; }
    public float Rotation { get; set; }
    public string EnemyType { get; set; } = "Guard";
    public List<PatrolWaypoint> PatrolPath { get; set; } = new();
    public bool ShowPatrolPath { get; set; } = true;
    /// <summary>When true, the enemy spawns already in <see cref="EnemyState.CORPSE"/>.</summary>
    public bool StartsAsCorpse { get; set; }

    /// <summary>When true, killing this enemy spawns an ammo pickup at its death tile.</summary>
    public bool DropsAmmo { get; set; }
}
