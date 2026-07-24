using Game.Features.Enemies.Behaviors;

namespace Game.Features.Enemies;

/// <summary>Maps <see cref="EnemyKind"/> to a shared behavior instance.</summary>
public static class EnemyBehaviorRegistry
{
    private static readonly GuardBehavior Guard = new();
    private static readonly DogBehavior Dog = new();

    public static IEnemyBehavior Get(EnemyKind kind) => kind switch
    {
        EnemyKind.Dog => Dog,
        _ => Guard
    };
}
