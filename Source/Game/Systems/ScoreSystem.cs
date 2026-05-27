using Game.Entities;
using Game.Scoring;

namespace Game.Systems;

/// <summary>Tracks Wolf3D-aligned runtime score and level-run counters during play mode.</summary>
public sealed class ScoreSystem
{
    private LevelScoringMetadata _metadata = LevelScoringMetadata.Default;

    public int LevelScore { get; private set; }
    public int Kills { get; private set; }
    public int TreasuresCollected { get; private set; }
    public int SecretsFound { get; private set; }
    public float ElapsedActiveSeconds { get; private set; }

    public int TotalKillableEnemies { get; private set; }
    public int TotalTreasures { get; private set; }
    public int TotalSecrets { get; private set; }

    public float KillRatio =>
        TotalKillableEnemies > 0 ? 100f * Kills / TotalKillableEnemies : 100f;

    public float TreasureRatio =>
        TotalTreasures > 0 ? 100f * TreasuresCollected / TotalTreasures : 100f;

    public float SecretRatio =>
        TotalSecrets > 0 ? 100f * SecretsFound / TotalSecrets : 100f;

    public void ResetForLevel(MapData mapData, LevelScoringMetadata? metadata = null)
    {
        _metadata = metadata ?? LevelScoringMetadata.Default;

        LevelScore = 0;
        Kills = 0;
        TreasuresCollected = 0;
        SecretsFound = 0;
        ElapsedActiveSeconds = 0f;

        TotalKillableEnemies = mapData.Enemies.Count(e => !e.StartsAsCorpse);
        TotalTreasures = mapData.Pickups.Count(p => TreasureScoreCatalog.IsTreasure(p.Type));
        TotalSecrets = 0;
    }

    public void Tick(float deltaTime)
    {
        if (deltaTime > 0f)
            ElapsedActiveSeconds += deltaTime;
    }

    public void OnEnemyKilled(EnemyKind kind)
    {
        Kills++;
        LevelScore += EnemyScoreCatalog.GetPoints(kind);
    }

    public void OnTreasureCollected(PickupType type)
    {
        int points = TreasureScoreCatalog.GetPoints(type);
        if (points <= 0)
            return;

        TreasuresCollected++;
        LevelScore += points;
    }

    public void OnSecretFound()
    {
        SecretsFound++;
    }

    /// <summary>Completion bonuses at level exit (Phase 2).</summary>
    public int ComputeCompletionBonuses()
    {
        int bonus = 0;

        if (TotalKillableEnemies > 0 && Kills >= TotalKillableEnemies)
            bonus += ScoringConstants.PerfectCategoryBonus;

        if (TotalSecrets > 0 && SecretsFound >= TotalSecrets)
            bonus += ScoringConstants.PerfectCategoryBonus;

        if (TotalTreasures > 0 && TreasuresCollected >= TotalTreasures)
            bonus += ScoringConstants.PerfectCategoryBonus;

        if (_metadata.ParTimeSeconds is int parTime)
        {
            int secondsUnderPar = parTime - (int)MathF.Floor(ElapsedActiveSeconds);
            if (secondsUnderPar > 0)
                bonus += secondsUnderPar * ScoringConstants.ParSecondBonus;
        }

        if (_metadata.IsSecretLevel)
            bonus += ScoringConstants.SecretLevelBonus;

        if (_metadata.IsBossLevel)
            bonus += ScoringConstants.BossLevelBonus;

        return bonus;
    }
}
