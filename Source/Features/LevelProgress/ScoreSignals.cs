using Game.Features.Enemies;
using Game.Features.Pickups;

namespace Game.Features.LevelProgress;

/// <summary>
/// Score write signals from gameplay producers (enemies, pickups, secrets).
/// <see cref="ScoreSystem"/> subscribes; producers never hold ScoreSystem.
/// </summary>
public sealed class ScoreSignals
{
    public event Action<EnemyKind>? EnemyKilled;
    public event Action<PickupType>? TreasureCollected;
    public event Action? SecretFound;

    public void NotifyEnemyKilled(EnemyKind kind) => EnemyKilled?.Invoke(kind);

    public void NotifyTreasureCollected(PickupType type) => TreasureCollected?.Invoke(type);

    public void NotifySecretFound() => SecretFound?.Invoke();
}
