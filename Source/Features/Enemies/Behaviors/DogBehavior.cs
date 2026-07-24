namespace Game.Features.Enemies.Behaviors;

/// <summary>
/// Fast melee chaser: keeps closing on the live player while visible, bites in close range.
/// No hitscan stand-and-shoot.
/// </summary>
public sealed class DogBehavior : IEnemyBehavior
{
    public void UpdateAttacking(Enemy enemy, IEnemyBehaviorServices world, float deltaTime)
    {
        if (!world.IsPlayerTargetable)
        {
            world.TryStartChaseToLastKnown(enemy);
            return;
        }

        if (enemy.CanSeePlayer)
        {
            enemy.LastSeenPlayerPosition = world.PlayerPosition;
            world.RotateTowardPlayer(enemy, deltaTime);

            float distTiles = world.DistanceToPlayerTiles(enemy);
            float meleeRange = enemy.Definition.Attack.MeleeRangeTiles;

            if (distTiles > meleeRange)
            {
                enemy.PathRefreshTimer -= deltaTime;
                if (enemy.PathRefreshTimer <= 0f || enemy.ChasePath.Count == 0)
                {
                    enemy.PathRefreshTimer = enemy.Definition.ChasePathRefreshSeconds;
                    world.RepathTo(enemy, world.PlayerPosition);
                }

                if (enemy.ChasePath.Count > 0)
                    world.FollowChasePath(enemy, deltaTime);
                else
                    world.MoveInCombat(enemy, world.PlayerPosition, deltaTime);
            }

            if (enemy.IsShooting && !enemy.WasShooting && distTiles <= meleeRange)
            {
                if (world.TryMeleePlayer(enemy))
                    world.PlayEnemyFiredFeedback();
            }

            enemy.WasShooting = enemy.IsShooting;
            return;
        }

        if (enemy.LastSeenPlayerPosition.HasValue)
            world.TryStartChaseToLastKnown(enemy);
        else
            world.ReturnToPatrol(enemy);
    }
}
