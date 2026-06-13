namespace Game.Features.Highscores.Shared;

/// <summary>Payload sent to the highscore API when a level run is submitted.</summary>
public sealed class ScoreSubmission
{
    public string LevelId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public int FinalScore { get; set; }
    public int LevelScore { get; set; }
    public int Kills { get; set; }
    public int TreasuresCollected { get; set; }
    public int SecretsFound { get; set; }
    public float ElapsedSeconds { get; set; }
    public string Checksum { get; set; } = string.Empty;
}
