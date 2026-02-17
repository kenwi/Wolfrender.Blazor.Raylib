using System.Numerics;
using Game.Entities;
using Game.Utilities;

namespace Game.Systems;

/// <summary>
/// Enemy AI system faithful to the original Wolfenstein 3D guard behavior.
///
/// Original Wolf3D guard state machine (from WL_ACT2.C / WL_STATE.C):
///
///   Stand/Patrol  ──[SightPlayer + reaction delay]──▶  FirstSighting (NOTICING)
///                                                           │
///                                                           ▼
///                                                   Chase (WALKING+alerted) ◄─┐
///                                                     │         │             │
///                                                     │   [random chance      │
///                                                     │    + line of sight]   │
///                                                     │         ▼             │
///                                                     │    Shoot (ATTACKING) ─┘
///                                                     │
///                                                     └──[lost player]──▶ Return to patrol/idle
///
/// Key behaviors from the original source:
///   - Guards patrol at base speed, chase at 3x speed (FirstSighting: ob->speed *= 3)
///   - Detection uses directional check + raycast + random reaction delay
///   - Chase uses SelectDodgeDir (randomized approach) when player visible
///   - Chase uses SelectChaseDir (direct approach) when player not visible
///   - Shooting has distance-based hit probability and damage
///   - Proximity auto-detects (MINSIGHT in original)
/// </summary>
public class EnemySystem
{
    private readonly Player _player;
    private readonly List<Enemy> _enemies;
    private readonly InputSystem _inputSystem;
    private readonly CollisionSystem _collisionSystem;
    private readonly DoorSystem _doorSystem;
    private MapData _mapData = null!;
    private readonly Random _rng = new();

    // Throttled LOS update
    private float _losAccumulator;
    private const float LosInterval = 1f / 11f;
    private const int FovRayCount = 48;

    // Movement constants
    private const float ArrivalThreshold = 0.5f;
    private const float EnemyCollisionRadius = 1.0f;
    private const float TurnSpeed = 4f;

    // Wolf3D timing: original runs at 70 Hz, 1 tic ≈ 1/70s
    private const float TicToSeconds = 1f / 70f;

    // Guard patrol speed and chase multiplier
    // Original: SPDPATROL = 512 units/tic. At 70Hz over 65536-unit tiles:
    //   Patrol = 0.55 tiles/sec, Chase = 1.64 tiles/sec (3x), Player ≈ 3+ tiles/sec
    // Our player moves at 5.0 units/sec (1.25 tiles/sec with QuadSize=4).
    // To preserve the original ratio (player ~2x faster than chasing guards):
    //   Chase ≈ 0.6 tiles/sec = 2.4 units/sec, Patrol ≈ 0.3 tiles/sec = 1.2 units/sec
    private const float PatrolSpeed = 1.2f;
    private const float ChaseSpeedMultiplier = 2f;

    // Reaction delay before first sighting
    // Original guard: temp2 = 1 + US_RndT()/4 tics (1–64 tics ≈ 0.01–0.91s)
    private const float MinReactionTime = 0.05f;
    private const float MaxReactionTime = 0.9f;

    // "Halt!" moment duration before entering chase
    private const float NoticingDuration = 0.4f;

    // Shooting sequence timing (original guard: shoot1=20, shoot2=20, shoot3=20 tics)
    private const float ShootAimDuration = 20f * TicToSeconds;
    private const float ShootFireDuration = 20f * TicToSeconds;
    private const float ShootRecoverDuration = 20f * TicToSeconds;

    // How often chase logic picks a new direction and evaluates shooting.
    // Original: T_Chase is called 4 times per 42-tic walk cycle (0.6s).
    // Each call picks a direction via SelectDodgeDir and may decide to shoot.
    // At our chase speed (2.4 u/s) a tile (4u) takes ~1.7s, so ~0.4s per think
    // gives roughly 4 thinks per tile — matching the original cadence.
    private const float ChaseThinkInterval = 0.4f;

    // A* path recomputation interval when chasing without line of sight
    private const float PathRefreshInterval = 0.5f;

    // Proximity auto-detect range in tiles (MINSIGHT in original ≈ 1.5 tiles)
    private const float ProximityDetectRange = 1.5f;

    public List<Enemy> Enemies => _enemies;

    public EnemySystem(Player player, InputSystem inputSystem, CollisionSystem collisionSystem, DoorSystem doorSystem)
    {
        _inputSystem = inputSystem;
        _player = player;
        _collisionSystem = collisionSystem;
        _doorSystem = doorSystem;
        _enemies = new List<Enemy>();
    }

    /// <summary>
    /// Rebuild the enemy list from MapData enemy placements.
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
                MoveSpeed = PatrolSpeed,
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
        // Throttled line-of-sight checks
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
    }

    // ─────────────────────────────────────────────────────────────────
    //  STATE MACHINE
    //  Maps to original Wolf3D states:
    //    IDLE      = s_grdstand    (T_Stand)
    //    WALKING   = s_grdpath     (T_Path)  or  s_grdchase (T_Chase)
    //    NOTICING  = FirstSighting transition
    //    ATTACKING = s_grdshoot    (T_Shoot)
    //    DYING     = s_grddie      (death animation)
    // ─────────────────────────────────────────────────────────────────

    private void UpdateBehavior(Enemy enemy, float deltaTime)
    {
        enemy.StateTimer += deltaTime;

        switch (enemy.EnemyState)
        {
            case EnemyState.IDLE:
                OnStand(enemy, deltaTime);
                break;

            case EnemyState.WALKING:
                if (enemy.IsAlerted)
                    OnChase(enemy, deltaTime);
                else
                    OnPatrol(enemy, deltaTime);
                break;

            case EnemyState.NOTICING:
                OnNoticing(enemy, deltaTime);
                break;

            case EnemyState.ATTACKING:
                OnShooting(enemy, deltaTime);
                break;

            case EnemyState.DYING:
                break;

            case EnemyState.COLLIDING:
                if (enemy.IsAlerted)
                    enemy.TransitionTo(EnemyState.WALKING);
                else if (enemy.HasPatrolPath)
                    enemy.TransitionTo(EnemyState.WALKING);
                else
                    enemy.TransitionTo(EnemyState.IDLE);
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  T_Stand — Standing guard, facing one direction
    //  Original: calls SightPlayer() each think cycle
    // ─────────────────────────────────────────────────────────────────

    private void OnStand(Enemy enemy, float deltaTime)
    {
        if (TrySightPlayer(enemy, deltaTime))
            return;

        if (enemy.HasPatrolPath)
            enemy.TransitionTo(EnemyState.WALKING);
    }

    // ─────────────────────────────────────────────────────────────────
    //  T_Path — Patrolling along waypoints
    //  Original: moves along turning-point path, calls SightPlayer()
    // ─────────────────────────────────────────────────────────────────

    private void OnPatrol(Enemy enemy, float deltaTime)
    {
        if (TrySightPlayer(enemy, deltaTime))
            return;

        if (enemy.HasPatrolPath)
            FollowPatrolPath(enemy, deltaTime);
    }

    // ─────────────────────────────────────────────────────────────────
    //  FirstSighting / NOTICING — The "Halt!" moment
    //  Original: plays alert sound, speed *= 3, sets FL_ATTACKMODE
    // ─────────────────────────────────────────────────────────────────

    private void OnNoticing(Enemy enemy, float deltaTime)
    {
        RotateTowardPlayer(enemy, deltaTime);

        if (enemy.StateTimer >= NoticingDuration)
        {
            // Enter attack/chase mode (FirstSighting from WL_STATE.C)
            enemy.IsAlerted = true;
            enemy.MoveSpeed = PatrolSpeed * ChaseSpeedMultiplier;
            enemy.LastSeenPlayerPosition = _player.Position;
            enemy.PathRefreshTimer = 0f;
            enemy.ChaseThinkTimer = 0f; // Think immediately on first chase frame
            enemy.CommittedMoveDirection = Vector3.Zero;
            enemy.TransitionTo(EnemyState.WALKING);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  T_Chase — Chasing / dodging toward the player
    //  Original: SelectDodgeDir when player visible (randomized approach)
    //            SelectChaseDir when player not visible (beeline)
    //            Random chance to stop and shoot each think cycle
    // ─────────────────────────────────────────────────────────────────

    private void OnChase(Enemy enemy, float deltaTime)
    {
        if (enemy.CanSeePlayer)
        {
            enemy.LastSeenPlayerPosition = _player.Position;
            RotateTowardPlayer(enemy, deltaTime);

            // Think timer: analogous to T_Chase being called ~4 times per tile.
            // Each "think" picks a new movement direction and may decide to shoot.
            // Between thinks, the enemy moves in its committed direction — no jitter.
            enemy.ChaseThinkTimer -= deltaTime;
            if (enemy.ChaseThinkTimer <= 0f)
            {
                enemy.ChaseThinkTimer = ChaseThinkInterval;

                // Should we stop and shoot?
                if (TryDecideToShoot(enemy))
                {
                    enemy.CommittedMoveDirection = Vector3.Zero;
                    enemy.TransitionTo(EnemyState.ATTACKING);
                    return;
                }

                // Pick a new movement direction (SelectDodgeDir equivalent)
                PickDodgeDirection(enemy);
            }

            // Move in the committed direction until the next think
            if (enemy.CommittedMoveDirection != Vector3.Zero)
                MoveInCommittedDirection(enemy, deltaTime);
        }
        else if (enemy.LastSeenPlayerPosition.HasValue)
        {
            // Lost sight — follow A* path to last known position (SelectChaseDir equivalent)
            FollowChasePath(enemy, deltaTime);
        }
        else
        {
            ReturnToPassive(enemy);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  T_Shoot — Shooting sequence
    //  Original guard: s_grdshoot1(aim,20) → s_grdshoot2(fire,20) → s_grdshoot3(recover,20) → chase
    //  The actual T_Shoot damage call happens during shoot2
    // ─────────────────────────────────────────────────────────────────

    private void OnShooting(Enemy enemy, float deltaTime)
    {
        RotateTowardPlayer(enemy, deltaTime);

        float totalDuration = ShootAimDuration + ShootFireDuration + ShootRecoverDuration;

        if (enemy.StateTimer >= totalDuration)
        {
            // Shooting sequence complete — back to chase
            enemy.TransitionTo(EnemyState.WALKING);
            return;
        }

        // Fire the actual shot at the aim → fire transition
        if (enemy.StateTimer >= ShootAimDuration &&
            enemy.StateTimer - deltaTime < ShootAimDuration)
        {
            FireAtPlayer(enemy);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  DETECTION — SightPlayer equivalent (WL_STATE.C)
    //
    //  Original two-phase detection:
    //    1. First sight sets temp2 = 1 + RndT()/4 (reaction delay in tics)
    //    2. Each subsequent call decrements temp2 by elapsed tics
    //    3. When temp2 reaches 0, FirstSighting() is called
    //
    //  This random delay is what makes guards feel "natural" — some
    //  react almost instantly, others take nearly a second to notice.
    // ─────────────────────────────────────────────────────────────────

    private bool TrySightPlayer(Enemy enemy, float deltaTime)
    {
        if (!enemy.CanSeePlayer)
        {
            enemy.ReactionTimer = -1f;
            return false;
        }

        if (enemy.ReactionTimer < 0f)
        {
            // First frame of detection — set random reaction delay
            float t = (float)_rng.NextDouble();
            enemy.ReactionTimer = MinReactionTime + t * (MaxReactionTime - MinReactionTime);
            return false;
        }

        enemy.ReactionTimer -= deltaTime;
        if (enemy.ReactionTimer <= 0f)
        {
            enemy.ReactionTimer = -1f;
            enemy.TransitionTo(EnemyState.NOTICING);
            return true;
        }

        return false;
    }

    // ─────────────────────────────────────────────────────────────────
    //  SHOOTING DECISION
    //  Original T_Chase: if (CheckLine(ob)) { if (US_RndT() < 128) → shoot }
    //  About 50% base chance per think, but we scale by distance
    //  so close guards are more aggressive (matching the feel of the original)
    // ─────────────────────────────────────────────────────────────────

    private bool TryDecideToShoot(Enemy enemy)
    {
        if (!enemy.CanSeePlayer)
            return false;

        float dist = enemy.DistanceFromPlayer;

        // Distance-scaled shoot probability per think cycle
        float shootChance = dist <= 1f ? 0.7f :
                            dist <= 3f ? 0.4f :
                            dist <= 6f ? 0.2f :
                                         0.1f;

        return _rng.NextDouble() < shootChance;
    }

    // ─────────────────────────────────────────────────────────────────
    //  FIRE AT PLAYER — T_Shoot damage calculation (WL_ACT2.C)
    //  Original: Manhattan distance determines hit chance and damage
    //    dist ≤ 1:  damage = US_RndT() >> 2  (0–63)
    //    dist 2–3:  lower chance, reduced damage
    //    dist 4+:   even lower chance, further reduced damage
    // ─────────────────────────────────────────────────────────────────

    private void FireAtPlayer(Enemy enemy)
    {
        if (!enemy.CanSeePlayer)
            return;

        float dist = enemy.DistanceFromPlayer;

        if (dist < 2f)
        {
            int damage = _rng.Next(0, 64);
            Debug.Log($"Guard fires! Distance: {dist:F1} tiles, Damage: {damage}");
        }
        else if (dist < 4f)
        {
            if (_rng.NextDouble() > 0.7)
                return; // miss
            int damage = _rng.Next(0, 32);
            Debug.Log($"Guard fires! Distance: {dist:F1} tiles, Damage: {damage}");
        }
        else
        {
            if (_rng.NextDouble() > 0.4)
                return; // miss
            int damage = _rng.Next(0, 16);
            Debug.Log($"Guard fires! Distance: {dist:F1} tiles, Damage: {damage}");
        }
        // TODO: Apply damage to player when player health system exists
    }

    // ─────────────────────────────────────────────────────────────────
    //  MOVEMENT — Dodge and chase directions
    //
    //  SelectDodgeDir (original):
    //    Arranges 5 preferred directions toward the player,
    //    then randomly swaps some to create a "dodging" effect
    //    so the guard doesn't just beeline at you.
    //
    //  SelectChaseDir (original):
    //    Direct approach — prefers the axis with greater distance,
    //    avoids turnaround, tries cardinal directions if blocked.
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pick a movement direction for the current think cycle.
    /// Inspired by SelectDodgeDir: arranges preferred directions toward the player,
    /// then randomly swaps some to create dodging. The enemy commits to this direction
    /// until the next think cycle fires.
    /// </summary>
    private void PickDodgeDirection(Enemy enemy)
    {
        Vector3 toPlayer = _player.Position - enemy.Position;
        float distXZ = MathF.Sqrt(toPlayer.X * toPlayer.X + toPlayer.Z * toPlayer.Z);

        if (distXZ < ArrivalThreshold)
        {
            enemy.CommittedMoveDirection = Vector3.Zero;
            return;
        }

        Vector3 forward = new Vector3(toPlayer.X / distXZ, 0, toPlayer.Z / distXZ);

        // SelectDodgeDir randomization: ~30% chance to strafe while approaching.
        // Original swaps the preferred cardinal directions randomly, creating
        // an approach that isn't a straight line but isn't erratic either.
        if (_rng.NextDouble() < 0.3)
        {
            Vector3 strafe = _rng.NextDouble() < 0.5
                ? new Vector3(-forward.Z, 0, forward.X)
                : new Vector3(forward.Z, 0, -forward.X);

            enemy.CommittedMoveDirection = Vector3.Normalize(forward + strafe);
        }
        else
        {
            enemy.CommittedMoveDirection = forward;
        }
    }

    /// <summary>
    /// Move the enemy along its committed direction. The direction was chosen by
    /// PickDodgeDirection and persists until the next think cycle, preventing
    /// frame-rate-dependent jitter.
    /// </summary>
    private void MoveInCommittedDirection(Enemy enemy, float deltaTime)
    {
        Vector3 direction = enemy.CommittedMoveDirection;
        float step = enemy.MoveSpeed * deltaTime;
        Vector3 nextPosition = enemy.Position + direction * step;

        if (_collisionSystem.CheckCollisionAtPosition(nextPosition, EnemyCollisionRadius))
        {
            TryOpenBlockingDoor(nextPosition);

            // If dodging was blocked, try moving directly toward the player instead
            Vector3 toPlayer = _player.Position - enemy.Position;
            float distXZ = MathF.Sqrt(toPlayer.X * toPlayer.X + toPlayer.Z * toPlayer.Z);
            if (distXZ > ArrivalThreshold)
            {
                Vector3 forward = new Vector3(toPlayer.X / distXZ, 0, toPlayer.Z / distXZ);
                nextPosition = enemy.Position + forward * step;
                if (!_collisionSystem.CheckCollisionAtPosition(nextPosition, EnemyCollisionRadius))
                {
                    enemy.Position = nextPosition;
                    enemy.CommittedMoveDirection = forward;
                    return;
                }
            }

            enemy.EnemyState = EnemyState.COLLIDING;
        }
        else
        {
            enemy.Position = nextPosition;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  PATROL — Follow waypoint path at patrol speed
    // ─────────────────────────────────────────────────────────────────

    private void FollowPatrolPath(Enemy enemy, float deltaTime)
    {
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
            enemy.Position = new Vector3(target.X, enemy.Position.Y, target.Z);
            enemy.CurrentWaypointIndex = (enemy.CurrentWaypointIndex + 1) % totalStops;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  A* CHASE PATH — Used when line of sight is lost
    //  Original doesn't have A*; it uses SelectChaseDir which tries
    //  cardinal directions toward the player's tile. A* is our modern
    //  adaptation that achieves the same "chase to last known position" goal.
    // ─────────────────────────────────────────────────────────────────

    private void FollowChasePath(Enemy enemy, float deltaTime)
    {
        enemy.PathRefreshTimer += deltaTime;
        if (enemy.ChasePath.Count == 0 || enemy.PathRefreshTimer >= PathRefreshInterval)
        {
            enemy.PathRefreshTimer = 0f;
            if (enemy.LastSeenPlayerPosition.HasValue)
                ComputeChasePath(enemy, enemy.LastSeenPlayerPosition.Value);
        }

        if (enemy.ChasePath.Count == 0 || enemy.ChasePathIndex >= enemy.ChasePath.Count)
        {
            // Arrived at last known position — player not found
            ReturnToPassive(enemy);
            return;
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
        }
    }

    /// <summary>
    /// Return enemy to passive (non-alerted) behavior.
    /// </summary>
    private void ReturnToPassive(Enemy enemy)
    {
        enemy.IsAlerted = false;
        enemy.MoveSpeed = PatrolSpeed;
        enemy.ReactionTimer = -1f;
        enemy.LastSeenPlayerPosition = null;
        enemy.ChasePath.Clear();
        enemy.ChasePathIndex = 0;
        enemy.CommittedMoveDirection = Vector3.Zero;
        enemy.ChaseThinkTimer = 0f;
        enemy.TransitionTo(enemy.HasPatrolPath ? EnemyState.WALKING : EnemyState.IDLE);
    }

    // ─────────────────────────────────────────────────────────────────
    //  SHARED HELPERS
    // ─────────────────────────────────────────────────────────────────

    private void RotateTowardPlayer(Enemy enemy, float deltaTime)
    {
        Vector3 toPlayer = _player.Position - enemy.Position;
        float targetAngle = MathF.Atan2(toPlayer.X, -toPlayer.Z) - MathF.PI / 2f;

        float diff = NormalizeAngle(targetAngle - enemy.Rotation);
        float maxStep = TurnSpeed * deltaTime;

        if (MathF.Abs(diff) <= maxStep)
            enemy.Rotation = targetAngle;
        else
            enemy.Rotation += MathF.Sign(diff) * maxStep;
    }

    private void MoveToward(Enemy enemy, Vector3 toTarget, float distXZ, float deltaTime)
    {
        Vector3 direction = new Vector3(toTarget.X / distXZ, 0, toTarget.Z / distXZ);
        float step = MathF.Min(enemy.MoveSpeed * deltaTime, distXZ);
        Vector3 nextPosition = enemy.Position + direction * step;

        if (_collisionSystem.CheckCollisionAtPosition(nextPosition, EnemyCollisionRadius))
        {
            enemy.EnemyState = EnemyState.COLLIDING;
            TryOpenBlockingDoor(nextPosition);
        }
        else
        {
            enemy.Position = nextPosition;
            enemy.Rotation = MathF.Atan2(direction.X, -direction.Z) - MathF.PI / 2f;
        }
    }

    private void TryOpenBlockingDoor(Vector3 position)
    {
        if (!_doorSystem.IsDoorBlocking(position, EnemyCollisionRadius))
            return;

        var doorSearchPos = new Vector2(
            position.X / LevelData.QuadSize,
            position.Z / LevelData.QuadSize);
        var closestDoor = _doorSystem.FindClosestDoor(doorSearchPos);
        if (closestDoor != null)
            _doorSystem.OpenDoor(closestDoor);
    }

    private void ComputeChasePath(Enemy enemy, Vector3 targetWorldPos)
    {
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
            _mapData, _doorSystem.Doors, sx, sy, sw, sh, enemyTile, targetTile);

        if (tilePath != null && tilePath.Count > 1)
        {
            enemy.ChasePath = tilePath.Skip(1).Select(t => new Vector3(
                t.X * quadSize,
                enemy.Position.Y,
                t.Y * quadSize)).ToList();
            enemy.ChasePathIndex = 0;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  SPRITE DIRECTION — 8-directional sprite selection
    // ─────────────────────────────────────────────────────────────────

    private void UpdateSpriteFrame(Enemy enemy)
    {
        Vector2 playerEnemyVector = new Vector2(
            enemy.Position.X - _player.Position.X,
            enemy.Position.Z - _player.Position.Z);

        var playerToEntityAngle = Math.Atan2(playerEnemyVector.X, playerEnemyVector.Y);

        while (playerToEntityAngle < 0) playerToEntityAngle += 2 * Math.PI;
        while (playerToEntityAngle >= 2 * Math.PI) playerToEntityAngle -= 2 * Math.PI;

        var relativeDirection = enemy.Rotation + playerToEntityAngle;

        while (relativeDirection < 0) relativeDirection += 2 * Math.PI;
        while (relativeDirection >= 2 * Math.PI) relativeDirection -= 2 * Math.PI;

        var rotatedAngle = relativeDirection + Math.PI / 2;
        while (rotatedAngle >= 2 * Math.PI) rotatedAngle -= 2 * Math.PI;

        var spriteIndex = (int)Math.Round(rotatedAngle / (Math.PI * 2) * 8) % 8;

        enemy.FrameColumnIndex = spriteIndex;
        enemy.AngleToPlayer = (float)rotatedAngle;
        enemy.DistanceFromPlayer = playerEnemyVector.Length() / LevelData.QuadSize;
    }

    // ─────────────────────────────────────────────────────────────────
    //  LINE OF SIGHT — CheckSight equivalent (WL_STATE.C)
    //
    //  Original CheckSight:
    //    1. Area connectivity check (areabyplayer[ob->areanumber])
    //    2. Proximity auto-detect (MINSIGHT ≈ 1.5 tiles)
    //    3. Direction check (player must be in facing hemisphere)
    //    4. CheckLine raycast through tilemap
    // ─────────────────────────────────────────────────────────────────

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
            if (enemy.EnemyState == EnemyState.DYING)
            {
                enemy.CanSeePlayer = false;
                continue;
            }

            var enemyTile = new Vector2(
                enemy.Position.X / quadSize + 0.5f,
                enemy.Position.Z / quadSize + 0.5f);

            float facingAngle = enemy.Rotation;

            // FOV polygon for minimap visualization
            enemy.FovPolygon = LineOfSight.GenerateFovPolygon(
                _mapData, doors, enemyTile, facingAngle,
                enemy.FovHalfAngle * 2f, enemy.SightRange, FovRayCount);

            float distTiles = Vector2.Distance(enemyTile, playerTile);

            // Proximity auto-detect (MINSIGHT equivalent)
            bool proximityDetect = distTiles < ProximityDetectRange;

            if (distTiles > enemy.SightRange && !proximityDetect)
            {
                enemy.CanSeePlayer = false;
                continue;
            }

            // FOV angle check (skip if proximity-detected)
            if (!proximityDetect)
            {
                Vector2 toPlayer = playerTile - enemyTile;
                float angleToPlayer = MathF.Atan2(toPlayer.Y, toPlayer.X);
                float angleDiff = NormalizeAngle(angleToPlayer - facingAngle);
                if (MathF.Abs(angleDiff) > enemy.FovHalfAngle)
                {
                    enemy.CanSeePlayer = false;
                    continue;
                }
            }

            bool couldSeeBefore = enemy.CanSeePlayer;

            // DDA raycast (CheckLine equivalent)
            enemy.CanSeePlayer = LineOfSight.CanSee(_mapData, doors, enemyTile, playerTile);

            if (enemy.CanSeePlayer)
            {
                enemy.LastSeenPlayerPosition = _player.Position;
            }
            else if (couldSeeBefore && enemy.IsAlerted)
            {
                // Just lost sight while chasing — compute path to last known position
                if (enemy.LastSeenPlayerPosition.HasValue)
                    ComputeChasePath(enemy, enemy.LastSeenPlayerPosition.Value);
            }
        }
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > MathF.PI) angle -= 2f * MathF.PI;
        while (angle < -MathF.PI) angle += 2f * MathF.PI;
        return angle;
    }
}
