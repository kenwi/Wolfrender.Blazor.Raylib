using Game.Utilities;

namespace Game.Scoring;

public static class EnemyScoreCatalog
{
    public static int GetPoints(EnemyKind kind) => kind switch
    {
        EnemyKind.Guard => 100,
        EnemyKind.Dog => 200,
        EnemyKind.Officer => 400,
        EnemyKind.Ss => 500,
        EnemyKind.Mutant => 700,
        EnemyKind.Boss => 5_000,
        _ => 100
    };

    /// <summary>Maps level JSON enemy type strings (case-insensitive).</summary>
    public static EnemyKind ParseKind(string? enemyType)
    {
        if (string.IsNullOrWhiteSpace(enemyType))
            return EnemyKind.Guard;

        if (Enum.TryParse<EnemyKind>(enemyType, ignoreCase: true, out var kind))
            return kind;

        Debug.Log($"Unknown EnemyType '{enemyType}' — scoring as Guard (100 pts).");
        return EnemyKind.Guard;
    }
}
