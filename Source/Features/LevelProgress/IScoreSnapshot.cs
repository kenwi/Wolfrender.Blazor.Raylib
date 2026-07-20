namespace Game.Features.LevelProgress;

/// <summary>
/// Read-only score view for HUD, highscores, and recording checksums.
/// </summary>
public interface IScoreSnapshot
{
    int LevelScore { get; }
    int FinalScore { get; }
    int CompletionBonus { get; }
    int Kills { get; }
    int TreasuresCollected { get; }
    int SecretsFound { get; }
    int TotalKillableEnemies { get; }
    int TotalTreasures { get; }
    int TotalSecrets { get; }
    float ElapsedActiveSeconds { get; }
    float KillRatio { get; }
    float TreasureRatio { get; }
    float SecretRatio { get; }
    bool IsFinalized { get; }
}
