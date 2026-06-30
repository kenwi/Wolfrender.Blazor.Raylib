namespace Game.Features.Recording;

public static class RecFileValidator
{
    public const int MaxEventCount = 500_000;

    public static bool TryValidateForReplay(RecFile rec, Func<string, bool> levelExists, out string error)
    {
        error = string.Empty;

        if (rec.Version < 1 || rec.Version > RecFile.CurrentVersion)
        {
            error = $"Unsupported recording version {rec.Version} (supported 1-{RecFile.CurrentVersion}).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(rec.LevelPath))
        {
            error = "Recording is missing levelPath.";
            return false;
        }

        if (!levelExists(rec.LevelPath))
        {
            error = $"Level file not found: '{rec.LevelPath}'. Type 'list-levels' to see available levels.";
            return false;
        }

        if (rec.Events.Count > MaxEventCount)
        {
            error = $"Recording has {rec.Events.Count} events (max {MaxEventCount}).";
            return false;
        }

        if (rec.UsesTickIndexedEvents)
        {
            for (int i = 0; i < rec.Events.Count; i++)
            {
                if (rec.Events[i].Tick < 1)
                {
                    error = $"Recording event {i} is missing a valid tick index.";
                    return false;
                }
            }
        }

        return true;
    }
}
