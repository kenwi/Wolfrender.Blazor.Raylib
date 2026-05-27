namespace Game.Scoring;

/// <summary>Per-level scoring options from level JSON (Phase 2 editor fields).</summary>
public sealed class LevelScoringMetadata
{
    public int? ParTimeSeconds { get; init; }
    public bool IsSecretLevel { get; init; }
    public bool IsBossLevel { get; init; }

    public static LevelScoringMetadata Default { get; } = new();
}
