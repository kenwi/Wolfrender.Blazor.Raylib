using System.Numerics;
using Raylib_cs;

namespace Game.Entities;

public enum EnemyState
{
    IDLE,
    WALKING,
    NOTICING,
    FLEEING,
    ATTACKING,
    /// <summary>Non-lethal hit reaction; shows first death frame then resumes <see cref="Enemy.ResumeStateAfterHit"/>.</summary>
    HIT,
    DYING,
    COLLIDING,
    /// <summary>Death animation finished; body lies here until linger time elapses.</summary>
    CORPSE
}

public class Enemy
{
    public float MaxHealth { get; set; } = 25f;
    public float Health { get; set; } = 25f;

    public Vector3 Position { get; set; }
    public float Rotation { get; set; }
    public float MoveSpeed { get; set; }
    public Rectangle FrameRect { get; set; }
    public int FrameColumnIndex { get; set; }
    public int FrameRowIndex { get; set; }
    public float AnimationTimer { get; set; }
    public int ShootingAnimationIndex { get; set; } = 1;
    public int DyingAnimationIndex { get; set; }
    public float AngleToPlayer  { get; set; }
    public float DistanceFromPlayer { get; set; }
    public EnemyState EnemyState { get; set; }

    /// <summary>
    /// Time the enemy has spent in the current state (seconds).
    /// Reset to 0 on every state transition via <see cref="TransitionTo"/>.
    /// </summary>
    public float StateTimer { get; set; }

    /// <summary>How long the body stays visible in <see cref="EnemyState.CORPSE"/> before removal (seconds).</summary>
    public float CorpseLingerSeconds { get; set; } = 30f;

    /// <summary>State to restore after <see cref="EnemyState.HIT"/> (set when entering hit reaction).</summary>
    public EnemyState ResumeStateAfterHit { get; set; } = EnemyState.IDLE;

    /// <summary>How long <see cref="EnemyState.HIT"/> lasts before resuming <see cref="ResumeStateAfterHit"/>.</summary>
    public float HitReactionDurationSeconds { get; set; } = 0.4f;

    /// <summary>
    /// Transition to a new state and reset the state timer.
    /// </summary>
    public void TransitionTo(EnemyState newState)
    {
        if (EnemyState == newState) return;
        EnemyState = newState;
        StateTimer = 0f;
    }

    /// <summary>True while the enemy participates in combat and collision (not during death or corpse).</summary>
    public bool IsCombatActive =>
        EnemyState != EnemyState.DYING && EnemyState != EnemyState.CORPSE;

    public void ApplyDamage(float amount)
    {
        if (!IsCombatActive || amount <= 0f)
            return;

        Health = MathF.Max(0f, Health - amount);
        if (Health <= 0f)
        {
            DyingAnimationIndex = 0;
            AnimationTimer = 0f;
            TransitionTo(EnemyState.DYING);
            return;
        }

        if (EnemyState == EnemyState.HIT)
        {
            StateTimer = 0f;
            return;
        }

        ResumeStateAfterHit = EnemyState;
        AnimationTimer = 0f;
        TransitionTo(EnemyState.HIT);
    }

    /// <summary>
    /// Patrol path as world-space waypoints. Empty list means no patrol.
    /// </summary>
    public List<Vector3> PatrolPath { get; set; } = new();

    /// <summary>
    /// Whether this enemy has a patrol path to follow.
    /// </summary>
    public bool HasPatrolPath => PatrolPath.Count > 0;

    /// <summary>
    /// Index of the current target waypoint in the patrol path.
    /// </summary>
    public int CurrentWaypointIndex { get; set; }

    /// <summary>
    /// The enemy's starting position, used to return after completing the patrol loop.
    /// </summary>
    public Vector3 PatrolOrigin { get; set; }

    // --- Line of Sight ---

    /// <summary>
    /// Whether the enemy can currently see the player.
    /// </summary>
    public bool CanSeePlayer { get; set; }

    /// <summary>
    /// The enemy's field-of-view half-angle in radians (total FOV = 2 * this).
    /// </summary>
    public float FovHalfAngle { get; set; } = MathF.PI / 3f; // 60 degrees half = 120 total

    /// <summary>
    /// Maximum sight range in tile units.
    /// </summary>
    public float SightRange { get; set; } = 12f;

    /// <summary>
    /// FOV polygon endpoints in tile-space for editor visualization.
    /// First point is the enemy's origin, subsequent points are ray hit positions.
    /// </summary>
    public List<Vector2> FovPolygon { get; set; } = new();

    // --- Pathfinding / Chase ---

    /// <summary>
    /// The last known world-space position where the enemy saw the player.
    /// Used as the pathfinding target after losing line of sight.
    /// </summary>
    public Vector3? LastSeenPlayerPosition { get; set; }

    /// <summary>
    /// World-space waypoints produced by A* pathfinding.
    /// </summary>
    public List<Vector3> ChasePath { get; set; } = new();

    /// <summary>
    /// Index of the current waypoint the enemy is walking toward in <see cref="ChasePath"/>.
    /// </summary>
    public int ChasePathIndex { get; set; }

    /// <summary>
    /// Accumulator used to throttle how often a new path is computed.
    /// </summary>
    public float PathRefreshTimer { get; set; }

    /// <summary>Seconds until this enemy may fire another hitscan shot at the player.</summary>
    public float AttackCooldownRemaining { get; set; }
}

public class EnemyGuard : Enemy
{
    public EnemyGuard()
    {
        MaxHealth = 25f;
        Health = MaxHealth;
        CorpseLingerSeconds = 30f;
        HitReactionDurationSeconds = 0.4f;
    }
}