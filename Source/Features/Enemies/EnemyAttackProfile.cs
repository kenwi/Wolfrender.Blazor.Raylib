namespace Game.Features.Enemies;

/// <summary>Combat numbers for an enemy kind (damage, range, accuracy).</summary>
public sealed class EnemyAttackProfile
{
    public required EnemyAttackKind Kind { get; init; }
    public required float Damage { get; init; }

    /// <summary>Hitscan: max aim error in radians to land a shot.</summary>
    public float AimToleranceRadians { get; init; } = 0.42f;

    /// <summary>0-1 chance a hitscan/melee swing that is otherwise valid deals damage.</summary>
    public float Accuracy { get; init; } = 1f;

    /// <summary>Melee: max distance in tiles to land a hit.</summary>
    public float MeleeRangeTiles { get; init; } = 1.25f;

    /// <summary>Preferred stand-off distance for hitscan enemies (tiles). Unused by melee.</summary>
    public float PreferredRangeTiles { get; init; } = 6f;
}
