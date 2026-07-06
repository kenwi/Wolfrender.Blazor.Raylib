namespace Game.Features.Recording;

/// <summary>
/// Per-component simulation state hash captured at a keyframe tick.
/// Component split lets divergence reports say what drifted (player vs enemies vs doors vs score).
/// Pure data - shared with the server, which stores but never computes checksums.
/// </summary>
public readonly record struct ChecksumKeyframe(long Tick, uint Player, uint Enemies, uint Doors, uint Score)
{
    public bool Matches(ChecksumKeyframe other) =>
        Player == other.Player
        && Enemies == other.Enemies
        && Doors == other.Doors
        && Score == other.Score;

    /// <summary>Names of components that differ from <paramref name="other"/>.</summary>
    public IReadOnlyList<string> DiffComponents(ChecksumKeyframe other)
    {
        var diffs = new List<string>(4);
        if (Player != other.Player)
            diffs.Add("player");
        if (Enemies != other.Enemies)
            diffs.Add("enemies");
        if (Doors != other.Doors)
            diffs.Add("doors");
        if (Score != other.Score)
            diffs.Add("score");
        return diffs;
    }
}
