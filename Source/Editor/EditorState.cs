using System.Numerics;
using Game.Systems;
using Game.Utilities;

namespace Game.Editor;

/// <summary>
/// Shared editor state and operations used by both the desktop ImGui editor
/// and the web Blazor editor. Pure C# with no UI framework dependencies.
/// </summary>
public class EditorState
{
    public const string EnemiesLayerName = "Enemies";

    public readonly MapData MapData;
    public readonly EnemySystem EnemySystem;
    public readonly DoorSystem DoorSystem;
    public readonly Entities.Player Player;
    public readonly EditorCamera Camera;
    public readonly List<EditorLayer> Layers;

    // Tile painting
    public int ActiveLayerIndex;
    public uint SelectedTileId = 1;

    // Cursor info
    public bool CursorInfoFollowsMouse;

    // Enemy editing
    public int HoveredEnemyIndex = -1;
    public int SelectedEnemyIndex = -1;
    public bool IsDraggingEnemy;

    // Patrol path editing
    public bool IsEditingPatrolPath;
    public int PatrolEditEnemyIndex = -1;
    public List<PatrolWaypoint> PatrolPathInProgress = new();

    // Simulation
    public bool IsSimulating;

    // Pathfinding visualizer
    public enum PathPickMode { None, Start, End }
    public PathPickMode PathPickingMode;
    public Vector2? PathStart;
    public Vector2? PathEnd;
    public List<Vector2>? PathResult;

    /// <summary>When true, the editor draws each live enemy's current A* chase path during simulation.</summary>
    public bool DrawEnemyPaths;

    /// <summary>When true, the editor draws live enemy FOV / line-of-sight cones during simulation.</summary>
    public bool DrawEnemyLineOfSight = true;

    // Mouse-over-UI flag (set by the active UI layer each frame)
    public bool IsMouseOverUI;

    // Status message
    public string StatusMessage = "";
    public float StatusTimer;

    // Fires when state changes that the Blazor UI should reflect
    public event Action? StateChanged;

    public EditorState(MapData mapData, EnemySystem enemySystem, DoorSystem doorSystem, Entities.Player player)
    {
        MapData = mapData;
        EnemySystem = enemySystem;
        DoorSystem = doorSystem;
        Player = player;
        Camera = new EditorCamera();
        Camera.CenterOnMap(mapData.Width, mapData.Height);

        Layers = new List<EditorLayer>
        {
            new() { Name = "Floor", Tiles = mapData.Floor },
            new() { Name = "Walls", Tiles = mapData.Walls },
            new() { Name = "Ceiling", Tiles = mapData.Ceiling, IsVisible = false },
            new() { Name = "Doors", Tiles = mapData.Doors },
            new() { Name = EnemiesLayerName, Tiles = Array.Empty<uint>() },
        };
    }

    public EditorLayer ActiveLayer => Layers[ActiveLayerIndex];
    public bool IsOnEnemyLayer => Layers[ActiveLayerIndex].Name == EnemiesLayerName;

    public void NotifyStateChanged() => StateChanged?.Invoke();

    public void PaintTile(int x, int y)
    {
        if (IsOnEnemyLayer) return;
        if (x < 0 || x >= MapData.Width || y < 0 || y >= MapData.Height) return;
        var layer = Layers[ActiveLayerIndex];
        layer.Tiles[MapData.Width * y + x] = SelectedTileId;
    }

    public void PlaceEnemy(int x, int y)
    {
        if (x < 0 || x >= MapData.Width || y < 0 || y >= MapData.Height) return;
        MapData.Enemies.Add(new EnemyPlacement
        {
            TileX = x, TileY = y, Rotation = 0, EnemyType = "Guard"
        });
        SelectedEnemyIndex = MapData.Enemies.Count - 1;
        IsDraggingEnemy = true;
        NotifyStateChanged();
    }

    public void SelectEnemy(int index)
    {
        SelectedEnemyIndex = index;
        IsDraggingEnemy = true;
        NotifyStateChanged();
    }

    public void MoveEnemy(int x, int y)
    {
        if (SelectedEnemyIndex < 0 || SelectedEnemyIndex >= MapData.Enemies.Count) return;
        if (x < 0 || x >= MapData.Width || y < 0 || y >= MapData.Height) return;
        MapData.Enemies[SelectedEnemyIndex].TileX = x;
        MapData.Enemies[SelectedEnemyIndex].TileY = y;
    }

    public void DeleteSelectedEnemy()
    {
        if (SelectedEnemyIndex < 0 || SelectedEnemyIndex >= MapData.Enemies.Count) return;
        MapData.Enemies.RemoveAt(SelectedEnemyIndex);
        SelectedEnemyIndex = -1;
        NotifyStateChanged();
    }

    public void AddPatrolWaypoint(int x, int y)
    {
        if (x < 0 || x >= MapData.Width || y < 0 || y >= MapData.Height) return;
        PatrolPathInProgress.Add(new PatrolWaypoint { TileX = x, TileY = y });
    }

    public void ConfirmPatrolPath()
    {
        if (PatrolEditEnemyIndex >= 0 && PatrolEditEnemyIndex < MapData.Enemies.Count)
        {
            MapData.Enemies[PatrolEditEnemyIndex].PatrolPath = new List<PatrolWaypoint>(PatrolPathInProgress);
        }
        IsEditingPatrolPath = false;
        PatrolPathInProgress.Clear();
        PatrolEditEnemyIndex = -1;
        NotifyStateChanged();
    }

    public void CancelPatrolPath()
    {
        IsEditingPatrolPath = false;
        PatrolPathInProgress.Clear();
        PatrolEditEnemyIndex = -1;
        NotifyStateChanged();
    }

    public void StartEditingPatrolPath()
    {
        if (SelectedEnemyIndex < 0) return;
        IsEditingPatrolPath = true;
        PatrolEditEnemyIndex = SelectedEnemyIndex;
        PatrolPathInProgress.Clear();
        NotifyStateChanged();
    }

    public void ToggleSimulation()
    {
        IsSimulating = !IsSimulating;
        if (IsSimulating)
        {
            EnemySystem.Rebuild(MapData.Enemies, MapData);
            DoorSystem.Rebuild(MapData.Doors, MapData.Width);
        }
        NotifyStateChanged();
    }

    /// <summary>
    /// Door interaction + animation during editor simulation (same as in-game <see cref="DoorSystem.Update"/>).
    /// </summary>
    public void UpdateDoorsDuringSimulation(float deltaTime, bool interactPressed)
    {
        var input = new InputState { IsInteractPressed = interactPressed };
        DoorSystem.Update(deltaTime, input, Player.Position, EnemySystem.Enemies);
    }

    public void ClearLevel()
    {
        int tileCount = MapData.Width * MapData.Height;
        MapData.Floor = new uint[tileCount];
        MapData.Walls = new uint[tileCount];
        MapData.Ceiling = new uint[tileCount];
        MapData.Doors = new uint[tileCount];
        MapData.Enemies.Clear();
        SelectedEnemyIndex = -1;
        HoveredEnemyIndex = -1;
        RefreshLayerReferences();
        SetStatus("New empty level created");
    }

    public void RefreshLayerReferences()
    {
        SelectedEnemyIndex = -1;
        HoveredEnemyIndex = -1;

        foreach (var layer in Layers)
        {
            layer.Tiles = layer.Name switch
            {
                "Floor" => MapData.Floor,
                "Walls" => MapData.Walls,
                "Ceiling" => MapData.Ceiling,
                "Doors" => MapData.Doors,
                _ => layer.Tiles
            };
        }
        NotifyStateChanged();
    }

    public void SetStatus(string message, float duration = 4f)
    {
        StatusMessage = message;
        StatusTimer = duration;
    }

    public void UpdateStatusTimer(float deltaTime)
    {
        if (StatusTimer > 0)
            StatusTimer -= deltaTime;
    }

    public void SwitchToEnemyLayer()
    {
        for (int i = 0; i < Layers.Count; i++)
        {
            if (Layers[i].Name == EnemiesLayerName)
            {
                ActiveLayerIndex = i;
                break;
            }
        }
    }

    // ─── Pathfinding visualizer ──────────────────────────────────────────────────

    public void StartPickingPathStart()
    {
        PathPickingMode = PathPickMode.Start;
        NotifyStateChanged();
    }

    public void StartPickingPathEnd()
    {
        PathPickingMode = PathPickMode.End;
        NotifyStateChanged();
    }

    public void CancelPathPicking()
    {
        if (PathPickingMode == PathPickMode.None) return;
        PathPickingMode = PathPickMode.None;
        NotifyStateChanged();
    }

    /// <summary>
    /// Set whichever endpoint is being picked, then recompute the path. Out-of-bounds clicks are ignored.
    /// </summary>
    public void SetPathPickPoint(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= MapData.Width || tileY < 0 || tileY >= MapData.Height) return;

        var point = new Vector2(tileX, tileY);
        switch (PathPickingMode)
        {
            case PathPickMode.Start: PathStart = point; break;
            case PathPickMode.End: PathEnd = point; break;
            default: return;
        }

        PathPickingMode = PathPickMode.None;
        RecomputePath();
        NotifyStateChanged();
    }

    public void ClearPath()
    {
        PathStart = null;
        PathEnd = null;
        PathResult = null;
        PathPickingMode = PathPickMode.None;
        NotifyStateChanged();
    }

    /// <summary>
    /// Recompute <see cref="PathResult"/> from the current start/end using the same A*
    /// the EnemySystem uses (so the visualizer shows exactly what the AI sees).
    /// </summary>
    public void RecomputePath()
    {
        PathResult = null;
        if (!PathStart.HasValue || !PathEnd.HasValue) return;

        var startTile = PathStart.Value;
        var endTile = PathEnd.Value;
        var (sx, sy, sw, sh) = Pathfinding.ComputeSliceBounds(
            startTile, endTile, MapData.Width, MapData.Height);
        PathResult = Pathfinding.FindPath(
            MapData, DoorSystem.Doors, sx, sy, sw, sh, startTile, endTile);
    }

    public void SwapLayers(int from, int to)
    {
        if (from < 0 || from >= Layers.Count || to < 0 || to >= Layers.Count) return;

        if (ActiveLayerIndex == from)
            ActiveLayerIndex = to;
        else if (ActiveLayerIndex == to)
            ActiveLayerIndex = from;

        (Layers[from], Layers[to]) = (Layers[to], Layers[from]);
        NotifyStateChanged();
    }
}
