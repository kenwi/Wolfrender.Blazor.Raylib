using System.Numerics;
using Game.Features.Players;

namespace Game.Features.LevelProgress;

/// <summary>
/// Level exit tiles on the wall layer: interact with E, swap art 44→42, then complete after a delay.
/// </summary>
public sealed class ExitSystem
{
    private readonly ScoreSystem _scoreSystem;
    private MapData _mapData = null!;
    private readonly List<ExitTile> _exits = new();

    private float _exitCountdownRemaining;
    private bool _levelComplete;

    public IReadOnlyList<ExitTile> Exits => _exits;
    public bool IsLevelComplete => _levelComplete;
    public bool IsExitPending => _exitCountdownRemaining > 0f;
    public bool IsBlockingGameplay => IsExitPending || IsLevelComplete;
    public float ExitCountdownRemaining => _exitCountdownRemaining;

    public ExitSystem(ScoreSystem scoreSystem) => _scoreSystem = scoreSystem;

    public void Rebuild(MapData mapData)
    {
        _mapData = mapData;
        _exits.Clear();
        _exitCountdownRemaining = 0f;
        _levelComplete = false;

        for (int index = 0; index < mapData.Walls.Length; index++)
        {
            if (mapData.Walls[index] != ExitTileIds.Inactive)
                continue;

            var (col, row) = LevelData.GetColRow(index, mapData.Width);
            _exits.Add(new ExitTile { TileX = col, TileY = row });
        }
    }

    /// <summary>
    /// Returns true when this frame's interact was consumed (door should not also open).
    /// </summary>
    public bool Update(float deltaTime, InputState input, Player player)
    {
        if (_levelComplete)
            return false;

        if (_exitCountdownRemaining > 0f)
        {
            _exitCountdownRemaining = MathF.Max(0f, _exitCountdownRemaining - deltaTime);
            if (_exitCountdownRemaining <= 0f)
                CompleteLevel();
            return false;
        }

        if (!input.IsInteractPressed || !player.IsAlive)
            return false;

        return TryActivateExit(player);
    }

    private bool TryActivateExit(Player player)
    {
        var exit = FindClosestInactiveExit(player);
        if (exit is null)
            return false;

        ActivateExit(exit);
        return true;
    }

    private ExitTile? FindClosestInactiveExit(Player player)
    {
        if (_exits.Count == 0)
            return null;

        float quadSize = LevelData.QuadSize;
        var playerTile = new Vector2(player.Position.X / quadSize, player.Position.Z / quadSize);

        ExitTile? closest = null;
        float closestDistance = float.MaxValue;

        foreach (var exit in _exits)
        {
            if (exit.IsActivated)
                continue;

            var exitTile = new Vector2(exit.TileX, exit.TileY);
            float distance = Vector2.Distance(playerTile, exitTile);
            if (distance > ExitTileIds.InteractRadiusTiles || distance >= closestDistance)
                continue;

            closest = exit;
            closestDistance = distance;
        }

        return closest;
    }

    private void ActivateExit(ExitTile exit)
    {
        int index = LevelData.GetIndex(exit.TileX, exit.TileY, _mapData.Width);
        _mapData.Walls[index] = ExitTileIds.Activated;
        exit.IsActivated = true;
        _exitCountdownRemaining = ExitTileIds.ExitDelaySeconds;

        Debug.Log(
            $"Exit activated at tile ({exit.TileX}, {exit.TileY}). " +
            $"Leaving in {ExitTileIds.ExitDelaySeconds:0.#}s.");
    }

    private void CompleteLevel()
    {
        _levelComplete = true;
        _scoreSystem.FinalizeLevel();
        Debug.Log($"Level complete. Final score: {_scoreSystem.FinalScore}.");
    }
}
