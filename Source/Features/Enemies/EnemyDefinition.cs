namespace Game.Features.Enemies;

/// <summary>
/// Data-driven roster entry for an <see cref="EnemyKind"/> (stats, senses, attack, spritesheet index).
/// Behavior is resolved separately via <see cref="EnemyBehaviorRegistry"/>.
/// </summary>
public sealed class EnemyDefinition
{
    public required EnemyKind Kind { get; init; }
    public required string DisplayName { get; init; }
    public required float MaxHealth { get; init; }
    public required float MoveSpeed { get; init; }
    public required float SightRangeTiles { get; init; }
    public required float FovHalfAngleRadians { get; init; }
    public required float TurnSpeedRadians { get; init; }
    public required float NoticingDurationSeconds { get; init; }
    public required float HitReactionDurationSeconds { get; init; }
    public required float CorpseLingerSeconds { get; init; }
    public required int ScorePoints { get; init; }
    public required EnemyAttackProfile Attack { get; init; }

    /// <summary>Index into <see cref="MapData.GameTextures"/> for this kind's spritesheet.</summary>
    public required int TextureIndex { get; init; }

    /// <summary>How often a chasing melee enemy re-runs A* toward the live player (seconds).</summary>
    public float ChasePathRefreshSeconds { get; init; } = 0.4f;
}
