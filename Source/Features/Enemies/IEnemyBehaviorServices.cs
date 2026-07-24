using System.Numerics;

namespace Game.Features.Enemies;

/// <summary>
/// Facade exposed to <see cref="IEnemyBehavior"/> implementations so they can move, aim, and deal damage
/// without depending on the full <see cref="EnemySystem"/> surface.
/// </summary>
public interface IEnemyBehaviorServices
{
    bool IsPlayerTargetable { get; }
    Vector3 PlayerPosition { get; }
    Random Rng { get; }

    void RotateTowardPlayer(Enemy enemy, float deltaTime);
    void MoveInCombat(Enemy enemy, Vector3 worldTarget, float deltaTime);
    void RepathTo(Enemy enemy, Vector3 worldTarget);
    void FollowChasePath(Enemy enemy, float deltaTime);
    void TryStartChaseToLastKnown(Enemy enemy);
    void ReturnToPatrol(Enemy enemy);

    float DistanceToPlayerTiles(Enemy enemy);
    bool TryHitscanPlayer(Enemy enemy);
    bool TryMeleePlayer(Enemy enemy);
    void PlayEnemyFiredFeedback();
}
