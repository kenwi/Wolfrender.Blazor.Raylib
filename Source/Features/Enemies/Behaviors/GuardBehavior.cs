using System.Numerics;

namespace Game.Features.Enemies.Behaviors;

/// <summary>Classic stand-still hitscan shooter once the player is visible.</summary>
public sealed class GuardBehavior : IEnemyBehavior
{
    public void UpdateAttacking(Enemy enemy, IEnemyBehaviorServices world, float deltaTime)
    {
        if (enemy.CanSeePlayer && world.IsPlayerTargetable)
        {
            enemy.LastSeenPlayerPosition = world.PlayerPosition;
            world.RotateTowardPlayer(enemy, deltaTime);

            if (enemy.IsShooting && !enemy.WasShooting)
            {
                Vector3 toPlayer = world.PlayerPosition - enemy.Position;
                float targetAngle = MathF.Atan2(toPlayer.X, -toPlayer.Z) - MathF.PI / 2f;
                float aimDiff = MathF.Abs(NormalizeAngle(targetAngle - enemy.Rotation));
                float aimTolerance = enemy.Definition.Attack.AimToleranceRadians;

                if (aimDiff <= aimTolerance)
                {
                    world.PlayEnemyFiredFeedback();
                    world.TryHitscanPlayer(enemy);
                }
            }

            enemy.WasShooting = enemy.IsShooting;
            return;
        }

        if (enemy.LastSeenPlayerPosition.HasValue)
            world.TryStartChaseToLastKnown(enemy);
        else
            world.ReturnToPatrol(enemy);
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > MathF.PI) angle -= 2f * MathF.PI;
        while (angle < -MathF.PI) angle += 2f * MathF.PI;
        return angle;
    }
}
