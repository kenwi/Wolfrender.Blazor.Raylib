namespace Game.Features.Enemies;

/// <summary>
/// Per-kind combat decisions while the shared state machine is in <see cref="EnemyState.ATTACKING"/>.
/// Patrol, chase-to-last-known, search, and sensing stay in <see cref="EnemySystem"/>.
/// </summary>
public interface IEnemyBehavior
{
    void UpdateAttacking(Enemy enemy, IEnemyBehaviorServices world, float deltaTime);
}
