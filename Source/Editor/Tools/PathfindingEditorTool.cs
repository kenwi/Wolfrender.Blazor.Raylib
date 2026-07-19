using System.Numerics;
using Game.Features.Doors;
using Game.Features.Enemies;

namespace Game.Editor;

/// <summary>
/// Desktop editor A* path visualizer (pick start/end, recompute with EnemySystem pathfinding).
/// </summary>
public sealed class PathfindingEditorTool
{
    public enum PathPickMode { None, Start, End }

    private readonly MapData _mapData;
    private readonly DoorSystem _doorSystem;
    private readonly Action _notifyChanged;
    private readonly Action _cancelSiblingPick;

    public PathPickMode PickingMode { get; private set; }
    public Vector2? PathStart { get; private set; }
    public Vector2? PathEnd { get; private set; }
    public List<Vector2>? PathResult { get; private set; }

    public PathfindingEditorTool(
        MapData mapData,
        DoorSystem doorSystem,
        Action notifyChanged,
        Action cancelSiblingPick)
    {
        _mapData = mapData;
        _doorSystem = doorSystem;
        _notifyChanged = notifyChanged;
        _cancelSiblingPick = cancelSiblingPick;
    }

    public void StartPickingStart()
    {
        _cancelSiblingPick();
        PickingMode = PathPickMode.Start;
        _notifyChanged();
    }

    public void StartPickingEnd()
    {
        _cancelSiblingPick();
        PickingMode = PathPickMode.End;
        _notifyChanged();
    }

    public void CancelPicking()
    {
        if (PickingMode == PathPickMode.None) return;
        PickingMode = PathPickMode.None;
        _notifyChanged();
    }

    /// <summary>Cancel pick without notifying (used when another tool starts picking).</summary>
    public void CancelPickingSilent()
    {
        PickingMode = PathPickMode.None;
    }

    /// <summary>
    /// Set whichever endpoint is being picked, then recompute the path. Out-of-bounds clicks are ignored.
    /// </summary>
    public void SetPickPoint(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= _mapData.Width || tileY < 0 || tileY >= _mapData.Height) return;

        var point = new Vector2(tileX, tileY);
        switch (PickingMode)
        {
            case PathPickMode.Start: PathStart = point; break;
            case PathPickMode.End: PathEnd = point; break;
            default: return;
        }

        PickingMode = PathPickMode.None;
        Recompute();
        _notifyChanged();
    }

    public void Clear()
    {
        PathStart = null;
        PathEnd = null;
        PathResult = null;
        PickingMode = PathPickMode.None;
        _notifyChanged();
    }

    /// <summary>
    /// Recompute <see cref="PathResult"/> using the same A* the EnemySystem uses.
    /// </summary>
    public void Recompute()
    {
        PathResult = null;
        if (!PathStart.HasValue || !PathEnd.HasValue) return;

        var startTile = PathStart.Value;
        var endTile = PathEnd.Value;
        var (sx, sy, sw, sh) = Pathfinding.ComputeSliceBounds(
            startTile, endTile, _mapData.Width, _mapData.Height);
        PathResult = Pathfinding.FindPath(
            _mapData, _doorSystem.Doors, sx, sy, sw, sh, startTile, endTile);
    }
}
