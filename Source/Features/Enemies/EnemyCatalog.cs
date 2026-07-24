using Game.Core;

namespace Game.Features.Enemies;

/// <summary>Static roster of enemy definitions (mirrors <c>WeaponCatalog</c>).</summary>
public static class EnemyCatalog
{
    private static readonly EnemyDefinition[] Definitions =
    [
        new()
        {
            Kind = EnemyKind.Guard,
            DisplayName = "Guard",
            MaxHealth = 25f,
            MoveSpeed = 2f,
            SightRangeTiles = 12f,
            FovHalfAngleRadians = MathF.PI / 3f,
            TurnSpeedRadians = 4f,
            NoticingDurationSeconds = 0.5f,
            HitReactionDurationSeconds = 0.4f,
            CorpseLingerSeconds = 30f,
            ScorePoints = 100,
            TextureIndex = GameTextureIndex.EnemyGuard,
            Attack = new EnemyAttackProfile
            {
                Kind = EnemyAttackKind.Hitscan,
                Damage = 9f,
                AimToleranceRadians = 0.42f,
                Accuracy = 1f,
                PreferredRangeTiles = 8f,
            },
        },
        new()
        {
            Kind = EnemyKind.Dog,
            DisplayName = "Dog",
            MaxHealth = 15f,
            MoveSpeed = 6.5f,
            SightRangeTiles = 8f,
            FovHalfAngleRadians = MathF.PI / 1.5f,
            TurnSpeedRadians = 7f,
            NoticingDurationSeconds = 0.0f,
            HitReactionDurationSeconds = 0.25f,
            CorpseLingerSeconds = 20f,
            ScorePoints = 200,
            // Dedicated dog sheet can replace this index when art is ready.
            TextureIndex = GameTextureIndex.EnemyDog,
            ChasePathRefreshSeconds = 0.25f,
            Attack = new EnemyAttackProfile
            {
                Kind = EnemyAttackKind.Melee,
                Damage = 12f,
                Accuracy = 0.85f,
                MeleeRangeTiles = 1f,
            },
        },
    ];

    public static EnemyDefinition Get(EnemyKind kind) =>
        Definitions.FirstOrDefault(d => d.Kind == kind)
        ?? Definitions.First(d => d.Kind == EnemyKind.Guard);

    public static IReadOnlyList<EnemyDefinition> All => Definitions;

    public static bool TryGet(EnemyKind kind, out EnemyDefinition definition)
    {
        definition = Definitions.FirstOrDefault(d => d.Kind == kind)!;
        return definition is not null;
    }
}
