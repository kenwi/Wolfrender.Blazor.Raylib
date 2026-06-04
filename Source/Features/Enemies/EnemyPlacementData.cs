namespace Game.Features.Enemies;

/// <summary>JSON DTO for <see cref="EnemyPlacement"/>.</summary>
public class EnemyPlacementData
{
    public int TileX { get; set; }
    public int TileY { get; set; }
    public float Rotation { get; set; }
    public string EnemyType { get; set; } = "Guard";
    public List<PatrolWaypointData> PatrolPath { get; set; } = new();
    public bool StartsAsCorpse { get; set; }
    public bool DropsAmmo { get; set; }
}
