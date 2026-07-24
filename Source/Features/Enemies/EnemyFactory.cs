namespace Game.Features.Enemies;

/// <summary>Builds runtime <see cref="Enemy"/> instances from level placements + catalog data.</summary>
public static class EnemyFactory
{
    public static Enemy Create(EnemyPlacement placement)
    {
        var kind = EnemyScoreCatalog.ParseKind(placement.EnemyType);
        var definition = EnemyCatalog.Get(kind);
        var startPos = LevelData.GetTileAnchorWorld(placement.TileX, placement.TileY, 2f);

        var enemy = new Enemy
        {
            Definition = definition,
            Behavior = EnemyBehaviorRegistry.Get(kind),
            ScoreKind = kind,
            Position = startPos,
            PatrolOrigin = startPos,
            Rotation = placement.Rotation,
            MaxHealth = definition.MaxHealth,
            Health = definition.MaxHealth,
            MoveSpeed = definition.MoveSpeed,
            FovHalfAngle = definition.FovHalfAngleRadians,
            SightRange = definition.SightRangeTiles,
            HitReactionDurationSeconds = definition.HitReactionDurationSeconds,
            CorpseLingerSeconds = definition.CorpseLingerSeconds,
            TextureIndex = definition.TextureIndex,
            DropsAmmoOnDeath = placement.DropsAmmo,
            CurrentWaypointIndex = 0,
            PatrolPath = placement.PatrolPath
                .Select(wp => LevelData.GetTileAnchorWorld(wp.TileX, wp.TileY, 2f))
                .ToList()
        };

        EnemySystem.ApplyPlacementSpawnState(enemy, placement);
        return enemy;
    }
}
