namespace Game.Features.Enemies;

/// <summary>JSON DTO for <see cref="EnemyPlacement"/>. Owns the mapping for this slice.</summary>
public class EnemyPlacementData
{
    public int TileX { get; set; }
    public int TileY { get; set; }
    public float Rotation { get; set; }
    public string EnemyType { get; set; } = "Guard";
    public List<PatrolWaypointData> PatrolPath { get; set; } = new();
    public bool StartsAsCorpse { get; set; }
    public bool DropsAmmo { get; set; }

    public static EnemyPlacementData FromPlacement(EnemyPlacement placement) => new()
    {
        TileX = placement.TileX,
        TileY = placement.TileY,
        Rotation = placement.Rotation,
        EnemyType = placement.EnemyType,
        PatrolPath = placement.PatrolPath.Select(PatrolWaypointData.FromWaypoint).ToList(),
        StartsAsCorpse = placement.StartsAsCorpse,
        DropsAmmo = placement.DropsAmmo
    };

    public EnemyPlacement ToPlacement() => new()
    {
        TileX = TileX,
        TileY = TileY,
        Rotation = Rotation,
        EnemyType = EnemyType,
        PatrolPath = PatrolPath.Select(w => w.ToWaypoint()).ToList(),
        StartsAsCorpse = StartsAsCorpse,
        DropsAmmo = DropsAmmo
    };
}
