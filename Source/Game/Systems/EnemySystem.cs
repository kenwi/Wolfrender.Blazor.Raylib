using System.Numerics;
using Game.Entities;
using Game.Utilities;

namespace Game.Systems;

public class EnemySystem
{
    private readonly Player _player;
    private readonly List<Enemy> _enemies;
    private readonly InputSystem _inputSystem;
    private readonly CollisionSystem _collisionSystem;
    private readonly DoorSystem _doorSystem;
    private readonly ICombatFeedback _combatFeedback;
    private readonly Random _rng = new();
    private MapData _mapData = null!;

    // Throttled LOS update
    private float _losAccumulator;
    private const float LosInterval = 1f / 11f; // ~11 checks per second
    private const int FovRayCount = 48;

    // Movement / behavior constants
    private const float ArrivalThreshold = 0.1f;
    private const float EnemyCollisionRadius = 1.0f;
    private const float TurnSpeed = 4f; // radians per second when facing player
    private const float NoticingDuration = 0.8f; // seconds before transitioning to ATTACKING

    private const float EnemyFireInterval = 0.85f;
    private const float EnemyFireAimTolerance = 0.42f; // radians (~24°)
    private const float EnemyShotDamage = 9f;
    private const float SearchSweepHalfAngle = 0.65f;
    private const float SearchSweepHoldSeconds = 0.85f;

    /// <summary>How long a fully-blocked slide must persist before the AI picks a different target.</summary>
    private const float StuckRecoverSeconds = 0.5f;

    public List<Enemy> Enemies => _enemies;

    public EnemySystem(
        Player player,
        InputSystem inputSystem,
        CollisionSystem collisionSystem,
        DoorSystem doorSystem,
        ICombatFeedback combatFeedback)
    {
        _inputSystem = inputSystem;
        _player = player;
        _collisionSystem = collisionSystem;
        _doorSystem = doorSystem;
        _combatFeedback = combatFeedback;
        _enemies = new List<Enemy>();
    }

    /// <summary>
    /// Rebuild the enemy list from MapData enemy placements.
    /// Call this when the level data has changed (e.g. after editing in the level editor).
    /// </summary>
    public void Rebuild(List<EnemyPlacement> placements, MapData? mapData = null)
    {
        if (mapData != null)
            _mapData = mapData;

        _enemies.Clear();
        _losAccumulator = 0f;

        foreach (var placement in placements)
        {
            var startPos = new Vector3(
                placement.TileX * LevelData.QuadSize,
                2f,
                placement.TileY * LevelData.QuadSize);

            var enemy = new EnemyGuard
            {
                Position = startPos,
                PatrolOrigin = startPos,
                Rotation = placement.Rotation,
                MoveSpeed = 2f,
                CurrentWaypointIndex = 0,
                PatrolPath = placement.PatrolPath.Select(wp => new Vector3(
                    wp.TileX * LevelData.QuadSize,
                    2f,
                    wp.TileY * LevelData.QuadSize)).ToList()
            };
            _enemies.Add(enemy);
        }
    }

    public void Update(float deltaTime)
    {
        // Throttled line-of-sight and FOV polygon update
        _losAccumulator += deltaTime;
        if (_losAccumulator >= LosInterval)
        {
            _losAccumulator -= LosInterval;
            UpdateLineOfSight();
        }

        foreach (var enemy in _enemies)
        {
            UpdateBehavior(enemy, deltaTime);
            UpdateSpriteFrame(enemy);
        }

        RemoveDeadEnemies();

        // Debug: cycle enemy state
        if (_inputSystem.GetInputState().IsChangeStatePressed)
        {
            foreach (var enemy in _enemies)
            {
                enemy.EnemyState++;
                var state = (int)enemy.EnemyState;
                enemy.EnemyState = (EnemyState)(state % 9);
            }
        }
    }

    private void UpdateBehavior(Enemy enemy, float deltaTime)
    {
        enemy.StateTimer += deltaTime;

        switch (enemy.EnemyState)
        {
            case EnemyState.IDLE:
                OnIdle(enemy, deltaTime);
                break;

            case EnemyState.WALKING:
                OnWalking(enemy, deltaTime);
                break;

            case EnemyState.NOTICING:
                OnNoticing(enemy, deltaTime);
                break;

            case EnemyState.ATTACKING:
                OnAttacking(enemy, deltaTime);
                break;

            case EnemyState.SEARCHING:
                OnSearching(enemy, deltaTime);
                break;

            case EnemyState.HIT:
                if (enemy.StateTimer >= enemy.HitReactionDurationSeconds)
                {
                    OrientTowardPlayerAfterHit(enemy);
                    enemy.TransitionTo(SanitizeResumeStateAfterHit(enemy));
                }
                break;

            case EnemyState.DYING:
                if (enemy.DyingAnimationIndex >= 4)
                    enemy.TransitionTo(EnemyState.CORPSE);
                break;

            case EnemyState.CORPSE:
                break;

            case EnemyState.COLLIDING:
                enemy.StuckTimer += deltaTime;
                if (enemy.StuckTimer >= StuckRecoverSeconds)
                {
                    enemy.StuckTimer = 0f;
                    TryRecoverFromStuck(enemy);
                }
                if (enemy.LastSeenPlayerPosition.HasValue)
                    FollowChasePath(enemy, deltaTime);
                else if (enemy.IsPatrolReturnPath)
                    FollowPatrolReturnPath(enemy, deltaTime);
                else if (enemy.HasPatrolPath)
                    UpdatePatrol(enemy, deltaTime);
                break;

            default:
                break;
        }
    }

    private static EnemyState SanitizeResumeStateAfterHit(Enemy enemy)
    {
        return enemy.ResumeStateAfterHit switch
        {
            EnemyState.DYING or EnemyState.CORPSE or EnemyState.HIT or EnemyState.SEARCHING => EnemyState.IDLE,
            EnemyState.COLLIDING => enemy.HasPatrolPath ? EnemyState.WALKING : EnemyState.IDLE,
            _ => enemy.ResumeStateAfterHit
        };
    }

    private void OnIdle(Enemy enemy, float deltaTime)
    {
        if (enemy.CanSeePlayer)
        {
            enemy.TransitionTo(EnemyState.NOTICING);
            return;
        }

        // Start patrolling if we have a path
        if (enemy.HasPatrolPath)
            enemy.TransitionTo(EnemyState.WALKING);
    }

    private void OnWalking(Enemy enemy, float deltaTime)
    {
        if (enemy.CanSeePlayer && _player.IsAlive)
        {
            enemy.IsPatrolReturnPath = false;
            enemy.TransitionTo(EnemyState.NOTICING);
            return;
        }

        if (enemy.LastSeenPlayerPosition.HasValue)
        {
            FollowChasePath(enemy, deltaTime);
            return;
        }

        if (enemy.IsPatrolReturnPath)
        {
            FollowPatrolReturnPath(enemy, deltaTime);
            return;
        }

        if (enemy.HasPatrolPath)
            UpdatePatrol(enemy, deltaTime);
    }

    private void OnNoticing(Enemy enemy, float deltaTime)
    {
        RotateTowardPlayer(enemy, deltaTime);

        if (!enemy.CanSeePlayer)
        {
            enemy.LastSeenPlayerPosition ??= _player.Position;
            TryStartChaseToLastKnown(enemy);
            return;
        }

        if (enemy.StateTimer >= NoticingDuration)
        {
            enemy.ResetShootingState();
            enemy.TransitionTo(EnemyState.ATTACKING);
            enemy.AttackCooldownRemaining = 0.35f;
        }
    }

    private void OnAttacking(Enemy enemy, float deltaTime)
    {
        if (enemy.CanSeePlayer)
        {
            if (!_player.IsAlive)
            {
                enemy.TransitionTo(EnemyState.WALKING);
                return;
            }

            // While we can see the player, keep updating the last known position
            enemy.LastSeenPlayerPosition = _player.Position;
            RotateTowardPlayer(enemy, deltaTime);

            if (enemy.IsShooting && !enemy.WasShooting)
            {
                Vector3 toPlayer = _player.Position - enemy.Position;
                float targetAngle = MathF.Atan2(toPlayer.X, -toPlayer.Z) - MathF.PI / 2f;
                float aimDiff = MathF.Abs(NormalizeAngle(targetAngle - enemy.Rotation));
                if (aimDiff <= EnemyFireAimTolerance)
                {
                    _combatFeedback.OnEnemyFired();
                    TryEnemyHitPlayer(enemy);
                }
            }

            enemy.WasShooting = enemy.IsShooting;
        }
        else if (enemy.LastSeenPlayerPosition.HasValue)
            TryStartChaseToLastKnown(enemy);
        else
            ReturnToPatrol(enemy);
    }

    private void OnSearching(Enemy enemy, float deltaTime)
    {
        if (enemy.CanSeePlayer && _player.IsAlive)
        {
            enemy.TransitionTo(EnemyState.NOTICING);
            return;
        }

        float targetAngle = enemy.SearchSweepStep switch
        {
            0 => enemy.SearchBaseRotation - SearchSweepHalfAngle,
            1 => enemy.SearchBaseRotation + SearchSweepHalfAngle,
            _ => enemy.SearchBaseRotation
        };

        RotateTowardAngle(enemy, targetAngle, deltaTime);

        if (enemy.StateTimer < SearchSweepHoldSeconds)
            return;

        enemy.SearchSweepStep++;
        enemy.StateTimer = 0f;
        if (enemy.SearchSweepStep > 2)
            ReturnToPatrol(enemy);
    }

    private void TryStartChaseToLastKnown(Enemy enemy)
    {
        if (!enemy.LastSeenPlayerPosition.HasValue)
        {
            ReturnToPatrol(enemy);
            return;
        }

        enemy.ResetShootingState();
        enemy.IsPatrolReturnPath = false;

        if (_mapData != null)
            ComputePathToTarget(enemy, enemy.LastSeenPlayerPosition.Value, ignoreDoors: false);

        if (IsNearLastSeenPosition(enemy))
            BeginSearchAtLastKnown(enemy);
        else
            enemy.TransitionTo(EnemyState.WALKING);
    }

    private void BeginSearchAtLastKnown(Enemy enemy)
    {
        if (enemy.LastSeenPlayerPosition.HasValue)
        {
            var lastSeen = enemy.LastSeenPlayerPosition.Value;
            var snapPos = new Vector3(lastSeen.X, enemy.Position.Y, lastSeen.Z);
            // Only snap if the destination is collision-free for the enemy. The player
            // has a smaller radius (0.8) than the enemy (1.0), so the exact last-seen
            // position can be valid for the player but force the enemy into a wall.
            if (!_collisionSystem.CheckCollisionAtPosition(snapPos, EnemyCollisionRadius))
                enemy.Position = snapPos;
        }

        enemy.ChasePath.Clear();
        enemy.ChasePathIndex = 0;
        enemy.SearchBaseRotation = enemy.Rotation;
        enemy.SearchSweepStep = 0;
        enemy.TransitionTo(EnemyState.SEARCHING);
    }

    private void ReturnToPatrol(Enemy enemy)
    {
        enemy.LastSeenPlayerPosition = null;
        enemy.ChasePath.Clear();
        enemy.ChasePathIndex = 0;
        enemy.IsPatrolReturnPath = false;
        enemy.SearchSweepStep = 0;
        enemy.ResetShootingState();

        if (enemy.HasPatrolPath && _mapData != null)
        {
            AlignPatrolWaypointToNearest(enemy);
            Vector3 patrolTarget = GetPatrolWaypointWorld(enemy);

            if (!IsNearWorldPosition(enemy.Position, patrolTarget))
                ComputePathToTarget(enemy, patrolTarget, ignoreDoors: true);

            enemy.IsPatrolReturnPath = enemy.ChasePath.Count > 0;
        }

        enemy.TransitionTo(enemy.HasPatrolPath ? EnemyState.WALKING : EnemyState.IDLE);
    }

    private static void AlignPatrolWaypointToNearest(Enemy enemy)
    {
        if (!enemy.HasPatrolPath) return;

        int totalStops = enemy.PatrolPath.Count + 1;
        int bestIndex = 0;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < totalStops; i++)
        {
            Vector3 waypoint = GetPatrolWaypointWorld(enemy, i);
            float dx = waypoint.X - enemy.Position.X;
            float dz = waypoint.Z - enemy.Position.Z;
            float distSq = dx * dx + dz * dz;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestIndex = i;
            }
        }

        enemy.CurrentWaypointIndex = bestIndex;
    }

    private static Vector3 GetPatrolWaypointWorld(Enemy enemy, int index)
    {
        int totalStops = enemy.PatrolPath.Count + 1;
        int idx = index % totalStops;
        return idx < enemy.PatrolPath.Count
            ? enemy.PatrolPath[idx]
            : enemy.PatrolOrigin;
    }

    private Vector3 GetPatrolWaypointWorld(Enemy enemy) =>
        GetPatrolWaypointWorld(enemy, enemy.CurrentWaypointIndex);

    private static bool IsNearWorldPosition(Vector3 position, Vector3 target)
    {
        float dx = position.X - target.X;
        float dz = position.Z - target.Z;
        return MathF.Sqrt(dx * dx + dz * dz) <= ArrivalThreshold;
    }

    private bool IsNearLastSeenPosition(Enemy enemy)
    {
        if (!enemy.LastSeenPlayerPosition.HasValue)
            return false;

        var lastSeen = enemy.LastSeenPlayerPosition.Value;
        float dx = enemy.Position.X - lastSeen.X;
        float dz = enemy.Position.Z - lastSeen.Z;
        return MathF.Sqrt(dx * dx + dz * dz) <= ArrivalThreshold;
    }

    private void TryEnemyHitPlayer(Enemy enemy)
    {
        if (_mapData == null || !_player.IsAlive)
            return;

        float quadSize = LevelData.QuadSize;
        var enemyTile = new Vector2(
            enemy.Position.X / quadSize + 0.5f,
            enemy.Position.Z / quadSize + 0.5f);
        var playerTile = new Vector2(
            _player.Position.X / quadSize + 0.5f,
            _player.Position.Z / quadSize + 0.5f);

        if (!LineOfSight.CanSee(_mapData, _doorSystem.Doors, enemyTile, playerTile))
            return;

        // if (_rng.NextSingle() > 0.72f)
        //     return;

        float healthBefore = _player.Health;
        _player.TakeDamage(EnemyShotDamage);
        if (_player.Health < healthBefore)
            _combatFeedback.OnPlayerDamaged(EnemyShotDamage);
    }

    private void RemoveDeadEnemies()
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _enemies[i];
            if (enemy.EnemyState == EnemyState.CORPSE && enemy.StateTimer >= enemy.CorpseLingerSeconds)
                _enemies.RemoveAt(i);
        }
    }

    /// <summary>
    /// Compute an A* path from the enemy's current position to a world-space target.
    /// </summary>
    private void ComputePathToTarget(Enemy enemy, Vector3 targetWorldPos, bool ignoreDoors)
    {
        // TODO: remove this once we have decided if we should care about doors or not
        ignoreDoors = true;

        if (_mapData == null) return;

        float quadSize = LevelData.QuadSize;
        var enemyTile = new Vector2(
            enemy.Position.X / quadSize + 0.5f,
            enemy.Position.Z / quadSize + 0.5f);
        var targetTile = new Vector2(
            targetWorldPos.X / quadSize + 0.5f,
            targetWorldPos.Z / quadSize + 0.5f);

        var (sx, sy, sw, sh) = Pathfinding.ComputeSliceBounds(
            enemyTile, targetTile, _mapData.Width, _mapData.Height);

        var tilePath = Pathfinding.FindPath(
            _mapData, _doorSystem.Doors, sx, sy, sw, sh, enemyTile, targetTile, ignoreDoors);

        enemy.ChasePath.Clear();
        enemy.ChasePathIndex = 0;

        if (tilePath != null && tilePath.Count > 1)
        {
            // Convert tile path to world-space waypoints (skip the first point — that's where we are)
            enemy.ChasePath = tilePath.Skip(1).Select(t => new Vector3(
                t.X * quadSize,
                enemy.Position.Y,
                t.Y * quadSize)).ToList();
        }
    }

    private void ComputeChasePath(Enemy enemy, Vector3 targetWorldPos) =>
        ComputePathToTarget(enemy, targetWorldPos, ignoreDoors: false);

    /// <summary>
    /// Walk along the chase path using MoveToward.
    /// When the path is finished, return to normal behavior.
    /// </summary>
    private void FollowChasePath(Enemy enemy, float deltaTime)
    {
        if (enemy.ChasePath.Count == 0 || enemy.ChasePathIndex >= enemy.ChasePath.Count)
        {
            if (IsNearLastSeenPosition(enemy))
            {
                BeginSearchAtLastKnown(enemy);
                return;
            }

            // No (or exhausted) chase path but we're still not at the last-seen tile.
            // Re-run A* to that tile instead of straight-lining through walls.
            if (enemy.LastSeenPlayerPosition.HasValue && _mapData != null)
                ComputeChasePath(enemy, enemy.LastSeenPlayerPosition.Value);

            // If A* couldn't produce a usable path (target blocked, unreachable, etc.),
            // give up the chase and search from here.
            if (enemy.ChasePath.Count == 0 || enemy.ChasePathIndex >= enemy.ChasePath.Count)
            {
                BeginSearchAtLastKnown(enemy);
                return;
            }

            // Otherwise fall through and follow the freshly-computed path this frame.
        }

        Vector3 target = enemy.ChasePath[enemy.ChasePathIndex];
        Vector3 toTarget = target - enemy.Position;
        float distXZ = MathF.Sqrt(toTarget.X * toTarget.X + toTarget.Z * toTarget.Z);

        if (distXZ > ArrivalThreshold)
        {
            MoveToward(enemy, toTarget, distXZ, deltaTime);
        }
        else
        {
            enemy.Position = new Vector3(target.X, enemy.Position.Y, target.Z);
            enemy.ChasePathIndex++;

            if (enemy.ChasePathIndex >= enemy.ChasePath.Count && IsNearLastSeenPosition(enemy))
                BeginSearchAtLastKnown(enemy);
        }
    }

    /// <summary>
    /// Follow the A* route back to the nearest patrol waypoint after search/chase ends.
    /// </summary>
    private void FollowPatrolReturnPath(Enemy enemy, float deltaTime)
    {
        if (enemy.ChasePath.Count == 0 || enemy.ChasePathIndex >= enemy.ChasePath.Count)
        {
            Vector3 patrolTarget = GetPatrolWaypointWorld(enemy);

            if (!IsNearWorldPosition(enemy.Position, patrolTarget) && _mapData != null)
                ComputePathToTarget(enemy, patrolTarget, ignoreDoors: true);

            if (enemy.ChasePath.Count == 0 || enemy.ChasePathIndex >= enemy.ChasePath.Count)
            {
                enemy.IsPatrolReturnPath = false;
                enemy.ChasePath.Clear();
                enemy.ChasePathIndex = 0;
                UpdatePatrol(enemy, deltaTime);
                return;
            }
        }

        Vector3 target = enemy.ChasePath[enemy.ChasePathIndex];
        Vector3 toTarget = target - enemy.Position;
        float distXZ = MathF.Sqrt(toTarget.X * toTarget.X + toTarget.Z * toTarget.Z);

        if (distXZ > ArrivalThreshold)
        {
            MoveToward(enemy, toTarget, distXZ, deltaTime);
        }
        else
        {
            enemy.Position = new Vector3(target.X, enemy.Position.Y, target.Z);
            enemy.ChasePathIndex++;

            if (enemy.ChasePathIndex >= enemy.ChasePath.Count)
            {
                enemy.IsPatrolReturnPath = false;
                enemy.ChasePath.Clear();
                enemy.ChasePathIndex = 0;
            }
        }
    }

    /// <summary>
    /// After a hit reaction, the enemy knows where the player is: face that direction immediately.
    /// </summary>
    private void OrientTowardPlayerAfterHit(Enemy enemy)
    {
        if (!_player.IsAlive)
            return;

        enemy.LastSeenPlayerPosition = _player.Position;
        enemy.Rotation = GetFacingAngleToward(enemy.Position, _player.Position);
    }

    private static float GetFacingAngleToward(Vector3 from, Vector3 to)
    {
        Vector3 toTarget = to - from;
        return MathF.Atan2(toTarget.X, -toTarget.Z) - MathF.PI / 2f;
    }

    /// <summary>
    /// Smoothly rotate the enemy to face the player's current position.
    /// </summary>
    private void RotateTowardPlayer(Enemy enemy, float deltaTime)
    {
        RotateTowardAngle(enemy, GetFacingAngleToward(enemy.Position, _player.Position), deltaTime);
    }

    private static void RotateTowardAngle(Enemy enemy, float targetAngle, float deltaTime)
    {
        float diff = NormalizeAngle(targetAngle - enemy.Rotation);
        float maxStep = TurnSpeed * deltaTime;

        if (MathF.Abs(diff) <= maxStep)
            enemy.Rotation = targetAngle;
        else
            enemy.Rotation += MathF.Sign(diff) * maxStep;
    }

    private void UpdatePatrol(Enemy enemy, float deltaTime)
    {
        // Build the full loop: origin -> waypoints -> back to origin
        int totalStops = enemy.PatrolPath.Count + 1;
        int idx = enemy.CurrentWaypointIndex % totalStops;

        Vector3 target = idx < enemy.PatrolPath.Count
            ? enemy.PatrolPath[idx]
            : enemy.PatrolOrigin;

        Vector3 toTarget = target - enemy.Position;
        float distXZ = MathF.Sqrt(toTarget.X * toTarget.X + toTarget.Z * toTarget.Z);

        if (distXZ > ArrivalThreshold)
        {
            MoveToward(enemy, toTarget, distXZ, deltaTime);
        }
        else
        {
            // Snap to target and advance to next waypoint
            enemy.Position = new Vector3(target.X, enemy.Position.Y, target.Z);
            enemy.CurrentWaypointIndex = (enemy.CurrentWaypointIndex + 1) % totalStops;
        }
    }

    /// <summary>
    /// Move the enemy toward a target direction, handling wall and door collisions.
    /// </summary>
    private void MoveToward(Enemy enemy, Vector3 toTarget, float distXZ, float deltaTime)
    {
        Vector3 direction = new Vector3(toTarget.X / distXZ, 0, toTarget.Z / distXZ);
        float step = MathF.Min(enemy.MoveSpeed * deltaTime, distXZ);
        Vector3 from = enemy.Position;
        Vector3 desired = from + direction * step;

        // Always face the intent direction so a sliding/blocked enemy keeps looking where it wants to go.
        enemy.Rotation = MathF.Atan2(direction.X, -direction.Z) - MathF.PI / 2f;

        // Open any closed door directly between us and the desired position so the slide
        // doesn't simply route around it without ever opening it.
        if (_doorSystem.IsDoorBlocking(desired, EnemyCollisionRadius))
            TryOpenBlockingDoor(desired);

        Vector3 resolved = _collisionSystem.ResolveMovement(from, desired, EnemyCollisionRadius);

        if ((resolved - from).LengthSquared() < 0.0001f)
        {
            enemy.EnemyState = EnemyState.COLLIDING;
            return;
        }

        enemy.Position = resolved;
        enemy.StuckTimer = 0f;
        enemy.EnemyState = EnemyState.WALKING;
    }

    /// <summary>
    /// Break a stuck-on-wall deadlock by giving the AI a fresh target so the slide
    /// direction changes. For chase: re-run A* from the current position. For patrol:
    /// advance to the next waypoint (the current one may be unreachable in a straight line).
    /// </summary>
    private void TryRecoverFromStuck(Enemy enemy)
    {
        if (enemy.LastSeenPlayerPosition.HasValue && _mapData != null)
        {
            ComputeChasePath(enemy, enemy.LastSeenPlayerPosition.Value);
            return;
        }

        if (enemy.IsPatrolReturnPath && _mapData != null)
        {
            ComputePathToTarget(enemy, GetPatrolWaypointWorld(enemy), ignoreDoors: true);
            return;
        }

        if (enemy.HasPatrolPath)
        {
            int totalStops = enemy.PatrolPath.Count + 1;
            enemy.CurrentWaypointIndex = (enemy.CurrentWaypointIndex + 1) % totalStops;
        }
    }

    /// <summary>
    /// If a door is blocking the enemy's path, try to open it.
    /// </summary>
    private void TryOpenBlockingDoor(Vector3 position)
    {
        if (!_doorSystem.IsDoorBlocking(position, EnemyCollisionRadius))
            return;

        var doorSearchPos = new Vector2(
            position.X / LevelData.QuadSize,
            position.Z / LevelData.QuadSize);
        var closestDoor = _doorSystem.FindClosestDoor(doorSearchPos);
        if (closestDoor != null)
        {
            _doorSystem.OpenDoor(closestDoor);
        }
    }

    /// <summary>
    /// Calculate the sprite column index based on the angle between the player's
    /// view direction and the enemy's facing direction.
    /// </summary>
    private void UpdateSpriteFrame(Enemy enemy)
    {
        if (enemy.EnemyState is EnemyState.DYING or EnemyState.CORPSE or EnemyState.HIT)
            return;

        Vector2 playerEnemyVector = new Vector2(
            enemy.Position.X - _player.Position.X,
            enemy.Position.Z - _player.Position.Z);

        var playerToEntityAngle = Math.Atan2(playerEnemyVector.X, playerEnemyVector.Y);

        // Normalize to [0, 2π)
        while (playerToEntityAngle < 0) playerToEntityAngle += 2 * Math.PI;
        while (playerToEntityAngle >= 2 * Math.PI) playerToEntityAngle -= 2 * Math.PI;

        var relativeDirection = enemy.Rotation + playerToEntityAngle;

        // Normalize to [0, 2π)
        while (relativeDirection < 0) relativeDirection += 2 * Math.PI;
        while (relativeDirection >= 2 * Math.PI) relativeDirection -= 2 * Math.PI;

        // Rotate 90 degrees to align sprite sheet columns
        var rotatedAngle = relativeDirection + Math.PI / 2;
        while (rotatedAngle >= 2 * Math.PI) rotatedAngle -= 2 * Math.PI;

        var spriteIndex = (int)Math.Round(rotatedAngle / (Math.PI * 2) * 8) % 8;

        enemy.FrameColumnIndex = spriteIndex;
        enemy.AngleToPlayer = (float)rotatedAngle;
        enemy.DistanceFromPlayer = playerEnemyVector.Length() / LevelData.QuadSize;
    }

    /// <summary>
    /// Check line of sight for all enemies and regenerate their FOV polygons.
    /// Called at a throttled rate (LosInterval).
    /// </summary>
    private void UpdateLineOfSight()
    {
        if (_mapData == null) return;

        float quadSize = LevelData.QuadSize;
        var playerTile = new Vector2(
            _player.Position.X / quadSize + 0.5f,
            _player.Position.Z / quadSize + 0.5f);

        var doors = _doorSystem.Doors;

        foreach (var enemy in _enemies)
        {
            if (enemy.EnemyState is EnemyState.DYING or EnemyState.CORPSE)
                continue;

            var enemyTile = new Vector2(
                enemy.Position.X / quadSize + 0.5f,
                enemy.Position.Z / quadSize + 0.5f);

            float facingAngle = enemy.Rotation;

            // Generate FOV polygon for visualization
            enemy.FovPolygon = LineOfSight.GenerateFovPolygon(
                _mapData, doors, enemyTile, facingAngle,
                enemy.FovHalfAngle * 2f, enemy.SightRange, FovRayCount);

            // Distance check
            float distTiles = Vector2.Distance(enemyTile, playerTile);
            if (distTiles > enemy.SightRange)
            {
                enemy.CanSeePlayer = false;
                continue;
            }

            // FOV angle check
            Vector2 toPlayer = playerTile - enemyTile;
            float angleToPlayer = MathF.Atan2(toPlayer.Y, toPlayer.X);
            float angleDiff = NormalizeAngle(angleToPlayer - facingAngle);
            if (MathF.Abs(angleDiff) > enemy.FovHalfAngle)
            {
                enemy.CanSeePlayer = false;
                continue;
            }

            bool couldSeeBefore = enemy.CanSeePlayer;
            // DDA ray check
            enemy.CanSeePlayer = LineOfSight.CanSee(_mapData, doors, enemyTile, playerTile);

            if (enemy.CanSeePlayer)
            {
                // Continuously track the player's position while visible
                enemy.LastSeenPlayerPosition = _player.Position;
            }
            else if (couldSeeBefore && enemy.LastSeenPlayerPosition.HasValue)
            {
                // Pre-compute chase path when LOS breaks (behavior uses it on next tick).
                // Include COLLIDING so an enemy that was wedged against a wall when the
                // player slipped out of sight still gets a fresh path.
                if (enemy.EnemyState is EnemyState.ATTACKING
                    or EnemyState.NOTICING
                    or EnemyState.WALKING
                    or EnemyState.SEARCHING
                    or EnemyState.COLLIDING)
                    ComputeChasePath(enemy, enemy.LastSeenPlayerPosition.Value);
            }
        }
    }

    /// <summary>
    /// Normalize an angle to [-PI, PI] range.
    /// </summary>
    private static float NormalizeAngle(float angle)
    {
        while (angle > MathF.PI) angle -= 2f * MathF.PI;
        while (angle < -MathF.PI) angle += 2f * MathF.PI;
        return angle;
    }
}
