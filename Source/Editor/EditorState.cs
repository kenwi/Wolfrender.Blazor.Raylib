using System.Numerics;
using Game.Core.Level;
using Game.Editor.Undo;
using Game.Engine.Rendering;
using Game.Features.Doors;
using Game.Features.Enemies;
using Game.Features.LevelProgress;
using Game.Features.Pickups;
using Game.Features.Players;
using Game.Features.WorldObjects;

namespace Game.Editor;

/// <summary>
/// Shared editor state and operations used by both the desktop ImGui editor
/// and the web Blazor editor. Pure C# with no UI framework dependencies.
/// </summary>
public class EditorState
{
    public const string EnemiesLayerName = "Enemies";
    public const string PickupsLayerName = "Pickups";
    public const string ObjectsLayerName = "Objects";
    public const string WallsLayerName = "Walls";

    public readonly MapData MapData;
    public readonly EnemySystem EnemySystem;
    public readonly DoorSystem DoorSystem;
    public readonly SecretSystem SecretSystem;
    public readonly Player Player;
    public readonly EditorCamera Camera;
    public readonly List<EditorLayer> Layers;
    public readonly EditorUndoStack UndoStack = new();

    private readonly ActorDoorOccupancyProbe _doorOccupancy = new();
    private readonly List<Vector3> _doorOccupancyOthers = new();

    public enum EditorToolMode { Paint, Select }

    // Tile painting / wall selection
    public int ActiveLayerIndex;
    public uint SelectedTileId = 1;
    public EditorToolMode ToolMode = EditorToolMode.Paint;

    // Cursor info
    public bool CursorInfoFollowsMouse;

    // Simulation
    public bool IsSimulating;

    public PathfindingEditorTool PathfindingTool { get; }
    public SoundPropagationEditorTool SoundPropagationTool { get; }
    public SecretWallEditorTool SecretWallTool { get; }
    public EnemyEditorTool EnemyTool { get; }
    public PickupEditorTool PickupTool { get; }
    public PlayerSpawnEditorTool PlayerSpawnTool { get; }

    public bool HoveredPlayer
    {
        get => PlayerSpawnTool.IsHovered;
        set => PlayerSpawnTool.IsHovered = value;
    }

    public bool IsPlayerSelected
    {
        get => PlayerSpawnTool.IsSelected;
        set => PlayerSpawnTool.IsSelected = value;
    }

    public bool IsDraggingPlayer
    {
        get => PlayerSpawnTool.IsDragging;
        set => PlayerSpawnTool.IsDragging = value;
    }

    public int HoveredEnemyIndex
    {
        get => EnemyTool.HoveredIndex;
        set => EnemyTool.HoveredIndex = value;
    }

    public int SelectedEnemyIndex
    {
        get => EnemyTool.SelectedIndex;
        set => EnemyTool.SelectedIndex = value;
    }

    public bool IsDraggingEnemy
    {
        get => EnemyTool.IsDragging;
        set => EnemyTool.IsDragging = value;
    }

    public bool IsEditingPatrolPath => EnemyTool.IsEditingPatrolPath;
    public int PatrolEditEnemyIndex => EnemyTool.PatrolEditEnemyIndex;
    public List<PatrolWaypoint> PatrolPathInProgress => EnemyTool.PatrolPathInProgress;

    public PickupType SelectedPickupType
    {
        get => PickupTool.SelectedType;
        set => PickupTool.SelectedType = value;
    }

    public int HoveredPickupIndex
    {
        get => PickupTool.HoveredIndex;
        set => PickupTool.HoveredIndex = value;
    }

    public int SelectedPickupIndex
    {
        get => PickupTool.SelectedIndex;
        set => PickupTool.SelectedIndex = value;
    }

    public bool IsDraggingPickup
    {
        get => PickupTool.IsDragging;
        set => PickupTool.IsDragging = value;
    }

    /// <summary>When true, draw translucent room regions over the map.</summary>
    public bool ShowRoomOverlay;

    public LevelRoomMap? RoomMap { get; private set; }

    /// <summary>When true, the editor draws each live enemy's current A* chase path during simulation.</summary>
    public bool DrawEnemyPaths;

    /// <summary>When true, the editor draws live enemy FOV / line-of-sight cones during simulation.</summary>
    public bool DrawEnemyLineOfSight = true;

    /// <summary>When false, authored patrol paths are hidden for all enemies (per-enemy toggle still applies when true).</summary>
    public bool ShowPatrolPaths = false;

    // Mouse-over-UI flag (set by the active UI layer each frame)
    public bool IsMouseOverUI;

    /// <summary>Shared screen position for player/enemy properties panels (web: CSS top/right; desktop: ImGui pos).</summary>
    public const int EntityPropertiesPanelWidth = 280;
    public int EntityPropertiesPanelTop { get; set; } = 500;
    public int EntityPropertiesPanelRight { get; set; } = 10;
    public bool IsDraggingEntityPropertiesPanel { get; private set; }

    private float _entityPropsDragPointerX;
    private float _entityPropsDragPointerY;
    private int _entityPropsDragStartTop;
    private int _entityPropsDragStartRight;

    // Undo batching for paint strokes
    private List<TileChange>? _paintStroke;

    // Status message
    public string StatusMessage = "";
    public float StatusTimer;

    /// <summary>Current level JSON filename used for quick save (e.g. level.json).</summary>
    public string LevelFilename { get; private set; } = Path.GetFileName(LevelCatalog.DefaultLevelPath);

    // Fires when state changes that the Blazor UI should reflect
    public event Action? StateChanged;

    public EditorState(MapData mapData, EnemySystem enemySystem, DoorSystem doorSystem, SecretSystem secretSystem, Player player)
    {
        MapData = mapData;
        EnemySystem = enemySystem;
        DoorSystem = doorSystem;
        SecretSystem = secretSystem;
        Player = player;
        Camera = new EditorCamera();
        Camera.CenterOnMap(mapData.Width, mapData.Height);

        PathfindingEditorTool? pathTool = null;
        SoundPropagationEditorTool? soundTool = null;
        pathTool = new PathfindingEditorTool(
            mapData,
            doorSystem,
            NotifyStateChanged,
            () => soundTool!.CancelPickSilent());
        soundTool = new SoundPropagationEditorTool(
            mapData,
            doorSystem,
            () => IsSimulating,
            msg => SetStatus(msg),
            NotifyStateChanged,
            () => pathTool!.CancelPickingSilent());
        PathfindingTool = pathTool;
        SoundPropagationTool = soundTool;

        EnemyEditorTool? enemyTool = null;
        PickupEditorTool? pickupTool = null;
        PlayerSpawnEditorTool? playerSpawnTool = null;
        enemyTool = new EnemyEditorTool(
            mapData,
            UndoStack,
            NotifyStateChanged,
            OnEnemyPlacementEdited,
            () =>
            {
                pickupTool!.Deselect();
                playerSpawnTool!.Deselect();
            });
        pickupTool = new PickupEditorTool(
            mapData,
            UndoStack,
            NotifyStateChanged,
            msg => SetStatus(msg),
            CanPlacePickupAt,
            () =>
            {
                enemyTool!.Deselect();
                playerSpawnTool!.Deselect();
            });
        playerSpawnTool = new PlayerSpawnEditorTool(
            mapData,
            player,
            UndoStack,
            NotifyStateChanged,
            () =>
            {
                enemyTool!.Deselect();
                pickupTool!.Deselect();
            });
        EnemyTool = enemyTool;
        PickupTool = pickupTool;
        PlayerSpawnTool = playerSpawnTool;

        SecretWallTool = new SecretWallEditorTool(
            mapData,
            UndoStack,
            NotifyStateChanged,
            RebuildRoomMap,
            () =>
            {
                EnemyTool.Deselect();
                PickupTool.Deselect();
                PlayerSpawnTool.Deselect();
            });

        Layers = new List<EditorLayer>
        {
            new() { Name = "Floor", Tiles = mapData.Floor },
            new() { Name = "Walls", Tiles = mapData.Walls },
            new() { Name = "Ceiling", Tiles = mapData.Ceiling, IsVisible = false },
            new() { Name = DoorsLayerName, Tiles = mapData.Doors },
            new() { Name = ObjectsLayerName, Tiles = mapData.Objects },
            new() { Name = EnemiesLayerName, Tiles = Array.Empty<uint>() },
            new() { Name = PickupsLayerName, Tiles = Array.Empty<uint>() },
        };

        ApplyPlayerSpawnFromMap();
        RebuildRoomMap();
    }

    public EditorLayer ActiveLayer => Layers[ActiveLayerIndex];

    public void SetActiveLayerIndex(int index)
    {
        if (index < 0 || index >= Layers.Count) return;
        ActiveLayerIndex = index;
        EditorTilePalette.SanitizeSelectedTile(this);
        NotifyStateChanged();
    }

    public void SanitizeSelectedTileForActiveLayer() =>
        EditorTilePalette.SanitizeSelectedTile(this);
    public bool IsOnEnemyLayer => Layers[ActiveLayerIndex].Name == EnemiesLayerName;
    public bool IsOnPickupLayer => Layers[ActiveLayerIndex].Name == PickupsLayerName;
    public bool IsOnDoorLayer => Layers[ActiveLayerIndex].Name == DoorsLayerName;
    public bool IsOnObjectLayer => Layers[ActiveLayerIndex].Name == ObjectsLayerName;
    public bool IsOnWallsLayer => Layers[ActiveLayerIndex].Name == WallsLayerName;
    public bool IsOnTileLayer => !IsOnEnemyLayer && !IsOnPickupLayer;
    public bool HasSelectedWall => SecretWallTool.HasSelection;
    public bool IsWallSelectMode => ToolMode == EditorToolMode.Select && IsOnWallsLayer;

    public const string DoorsLayerName = "Doors";

    public void NotifyStateChanged() => StateChanged?.Invoke();

    public bool CanUndo => UndoStack.CanUndo;
    public bool CanRedo => UndoStack.CanRedo;

    public bool Undo()
    {
        if (!UndoStack.CanUndo)
            return false;

        string description = UndoStack.UndoDescription!;
        UndoStack.Undo(this);
        SetStatus($"Undo: {description}");
        NotifyStateChanged();
        return true;
    }

    public bool Redo()
    {
        if (!UndoStack.CanRedo)
            return false;

        string description = UndoStack.RedoDescription!;
        UndoStack.Redo(this);
        SetStatus($"Redo: {description}");
        NotifyStateChanged();
        return true;
    }

    public void ApplyAfterMapMutation()
    {
        RefreshLayerReferences();
        if (IsSimulating)
        {
            EnemySystem.Rebuild(MapData.Enemies, MapData);
            DoorSystem.Rebuild(MapData.Doors, MapData.Width);
            SecretSystem.Rebuild(MapData);
        }
    }

    internal void RestoreMapFromJson(string json)
    {
        LevelSerializer.DeserializeFromJson(MapData, json);
        RefreshLayerReferences();
    }

    public void ExecuteMapMutation(Action mutate, string description)
    {
        string before = LevelSerializer.SerializeToJson(MapData);
        mutate();
        string after = LevelSerializer.SerializeToJson(MapData);
        UndoStack.Push(new ReplaceMapDataCommand(before, after, description));
        ApplyAfterMapMutation();
    }

    public void LoadLevelFromJson(string path)
    {
        ExecuteMapMutation(() => LevelSerializer.LoadFromJson(MapData, path), $"Load {Path.GetFileName(path)}");
        LevelFilename = Path.GetFileName(path);
        SetStatus($"Loaded from {path}");
    }

    public void LoadLevelFromTmx(string path)
    {
        ExecuteMapMutation(() => LevelSerializer.LoadFromTmx(MapData, path), $"Load {Path.GetFileName(path)}");
        SetStatus($"Loaded TMX from {path}");
    }

    public void LoadLevelFromBmp(string path)
    {
        ExecuteMapMutation(() => LevelSerializer.LoadFromBmp(MapData, path), $"Load {Path.GetFileName(path)}");
        SetStatus($"Loaded BMP from {path}");
    }

    public void LoadLevelFromJsonString(string json, string description)
    {
        ExecuteMapMutation(() => LevelSerializer.DeserializeFromJson(MapData, json), description);
    }

    public void OnEnemyPlacementEdited()
    {
        if (IsSimulating)
            EnemySystem.Rebuild(MapData.Enemies, MapData);
        NotifyStateChanged();
    }

    public Vector2 GetEntityPropertiesImGuiPos(int screenWidth) =>
        new(screenWidth - EntityPropertiesPanelRight - EntityPropertiesPanelWidth, EntityPropertiesPanelTop);

    public void SetEntityPropertiesFromImGuiPos(Vector2 pos, int screenWidth)
    {
        EntityPropertiesPanelTop = Math.Max(0, (int)pos.Y);
        EntityPropertiesPanelRight = Math.Max(0, screenWidth - (int)pos.X - EntityPropertiesPanelWidth);
    }

    public void BeginEntityPropertiesPanelDrag(float clientX, float clientY)
    {
        IsDraggingEntityPropertiesPanel = true;
        _entityPropsDragPointerX = clientX;
        _entityPropsDragPointerY = clientY;
        _entityPropsDragStartTop = EntityPropertiesPanelTop;
        _entityPropsDragStartRight = EntityPropertiesPanelRight;
    }

    public void UpdateEntityPropertiesPanelDrag(float clientX, float clientY)
    {
        if (!IsDraggingEntityPropertiesPanel) return;

        int dy = (int)(clientY - _entityPropsDragPointerY);
        int dx = (int)(clientX - _entityPropsDragPointerX);
        EntityPropertiesPanelTop = Math.Max(0, _entityPropsDragStartTop + dy);
        EntityPropertiesPanelRight = Math.Max(0, _entityPropsDragStartRight - dx);
        NotifyStateChanged();
    }

    public void EndEntityPropertiesPanelDrag()
    {
        if (!IsDraggingEntityPropertiesPanel) return;
        IsDraggingEntityPropertiesPanel = false;
        NotifyStateChanged();
    }

    /// <summary>At most one entity properties panel is shown: player spawn or selected enemy.</summary>
    public bool ShouldShowPlayerPropertiesPanel(bool windowVisible) =>
        windowVisible && IsPlayerSelected;

    public bool ShouldShowEnemyPropertiesPanel(bool windowVisible) =>
        windowVisible && SelectedEnemyIndex >= 0 && !IsPlayerSelected;

    public bool ShouldShowWallPropertiesPanel(bool windowVisible) =>
        windowVisible && IsWallSelectMode && HasSelectedWall;

    public void SetToolMode(EditorToolMode mode)
    {
        if (ToolMode == mode) return;
        ToolMode = mode;
        if (mode == EditorToolMode.Paint)
            SecretWallTool.ClearSelection();
        SetStatus(mode == EditorToolMode.Paint ? "Paint mode" : "Select mode (Walls layer)");
        NotifyStateChanged();
    }

    public void SelectWallTile(int tileX, int tileY) =>
        SecretWallTool.SelectTile(tileX, tileY, GetWallTileAt);

    public void ClearWallSelection() => SecretWallTool.ClearSelection();

    public SecretWallPlacement? FindSecretWallAt(int tileX, int tileY) =>
        SecretWallTool.FindAt(tileX, tileY);

    public SecretWallPlacement? GetSelectedSecretPlacement() =>
        SecretWallTool.GetSelectedPlacement();

    public bool IsSelectedWallSecret => SecretWallTool.IsSelectedSecret;

    public void SetWallSecret(bool isSecret, SecretWallDirection direction, int travelTiles) =>
        SecretWallTool.SetSecret(isSecret, direction, travelTiles);

    public void RemoveSecretWallAt(int tileX, int tileY) =>
        SecretWallTool.RemoveAt(tileX, tileY);

    public int GetMaxSecretTravelTiles(int tileX, int tileY, SecretWallDirection direction) =>
        SecretWallTool.GetMaxTravelTiles(tileX, tileY, direction);

    public int ClampSecretTravelTiles(int tileX, int tileY, SecretWallDirection direction, int travelTiles) =>
        SecretWallTool.ClampTravelTiles(tileX, tileY, direction, travelTiles);

    public void BeginPaintStroke()
    {
        _paintStroke ??= new List<TileChange>();
    }

    public void EndPaintStroke()
    {
        if (_paintStroke == null || _paintStroke.Count == 0)
        {
            _paintStroke = null;
            return;
        }

        UndoStack.Push(new PaintTilesCommand(_paintStroke));
        _paintStroke = null;
        NotifyStateChanged();
    }

    public void PaintTile(int x, int y)
    {
        if (IsOnEnemyLayer || IsOnPickupLayer) return;
        if (x < 0 || x >= MapData.Width || y < 0 || y >= MapData.Height) return;

        if (IsOnObjectLayer)
        {
            if (SelectedTileId != 0 && !CanPlaceObjectAt(x, y))
            {
                SetStatus("Cannot place object on walls or doors");
                return;
            }

            if (SelectedTileId != 0 && !ObjectSprites.IsValidObjectId(SelectedTileId))
                return;
        }
        else if (IsOnTileLayer && !EditorTilePalette.IsTileIdValidForLayer(SelectedTileId, ActiveLayer.Name))
            return;

        var layer = Layers[ActiveLayerIndex];
        int idx = MapData.Width * y + x;
        uint oldTile = layer.Tiles[idx];
        if (oldTile == SelectedTileId)
            return;

        if (_paintStroke != null
            && !_paintStroke.Exists(c => c.LayerIndex == ActiveLayerIndex && c.X == x && c.Y == y))
        {
            SecretWallPlacement? removedSecret = null;
            if (IsOnWallsLayer && SelectedTileId == 0)
            {
                var secret = SecretWallTool.FindAt(x, y);
                if (secret != null)
                {
                    removedSecret = new SecretWallPlacement
                    {
                        TileX = secret.TileX,
                        TileY = secret.TileY,
                        Direction = secret.Direction,
                        TravelTiles = secret.TravelTiles
                    };
                }
            }

            _paintStroke.Add(new TileChange(ActiveLayerIndex, x, y, oldTile, SelectedTileId, removedSecret));
        }

        layer.Tiles[idx] = SelectedTileId;

        if (IsOnWallsLayer && SelectedTileId == 0)
            SecretWallTool.RemoveAt(x, y);

        if (!IsOnEnemyLayer && !IsOnPickupLayer && !IsOnObjectLayer)
            RebuildRoomMap();
    }

    public void PlaceEnemy(int x, int y) => EnemyTool.Place(x, y);

    public void DeselectEnemy() => EnemyTool.Deselect();

    public void DeselectPickup() => PickupTool.Deselect();

    public void DeselectPlayer() => PlayerSpawnTool.Deselect();

    public void SelectPlayer() => PlayerSpawnTool.Select();

    public void EndPlayerDrag() => PlayerSpawnTool.EndDrag();

    /// <summary>Hide the yellow tile-under-cursor highlight when pointing at or dragging an entity.</summary>
    public bool ShouldShowTileHighlight() =>
        !HoveredPlayer && HoveredEnemyIndex < 0 && HoveredPickupIndex < 0
        && !IsDraggingPlayer && !IsDraggingEnemy && !IsDraggingPickup;

    public void SelectEnemy(int index) => EnemyTool.Select(index);

    public void BeginEnemyDrag() => EnemyTool.BeginDrag();

    public void EndEnemyDrag() => EnemyTool.EndDrag();

    public void MoveEnemy(int x, int y) => EnemyTool.MoveSelected(x, y);

    public void DeleteSelectedEnemy() => EnemyTool.DeleteSelected();

    public void DeleteEnemyAt(int index) => EnemyTool.DeleteAt(index);

    public void SetEnemyTilePosition(int index, int tileX, int tileY) =>
        EnemyTool.SetTilePosition(index, tileX, tileY);

    public void SetEnemyRotation(int index, float rotation) =>
        EnemyTool.SetRotation(index, rotation);

    public void SetEnemyType(int index, string enemyType) =>
        EnemyTool.SetType(index, enemyType);

    public void SetEnemyStartsAsCorpse(int index, bool value) =>
        EnemyTool.SetStartsAsCorpse(index, value);

    public void SetEnemyDropsAmmo(int index, bool value) =>
        EnemyTool.SetDropsAmmo(index, value);

    public void ClearEnemyPatrolPath(int index) =>
        EnemyTool.ClearPatrolPath(index);

    public int FindPickupIndexAt(int tileX, int tileY) =>
        PickupTool.FindIndexAt(tileX, tileY);

    public void PlacePickup(int x, int y) => PickupTool.Place(x, y);

    public void SelectPickup(int index) => PickupTool.Select(index);

    public void BeginPickupDrag() => PickupTool.BeginDrag();

    public void EndPickupDrag() => PickupTool.EndDrag();

    public void MovePickup(int x, int y) => PickupTool.MoveSelected(x, y);

    public void DeleteSelectedPickup() => PickupTool.DeleteSelected();

    public void DeletePickupAt(int index) => PickupTool.DeleteAt(index);

    public void SetPickupTilePosition(int index, int tileX, int tileY) =>
        PickupTool.SetTilePosition(index, tileX, tileY);

    public void SetPickupAmount(int index, int amount) =>
        PickupTool.SetAmount(index, amount);

    public void SetPickupType(int index, PickupType type) =>
        PickupTool.SetType(index, type);

    public void AddPatrolWaypoint(int x, int y) => EnemyTool.AddPatrolWaypoint(x, y);

    public void ConfirmPatrolPath() => EnemyTool.ConfirmPatrolPath();

    public void CancelPatrolPath() => EnemyTool.CancelPatrolPath();

    public void StartEditingPatrolPath() => EnemyTool.StartEditingPatrolPath();

    public void ToggleSimulation()
    {
        IsSimulating = !IsSimulating;
        if (IsSimulating)
        {
            EnemySystem.Rebuild(MapData.Enemies, MapData);
            DoorSystem.Rebuild(MapData.Doors, MapData.Width);
            SecretSystem.Rebuild(MapData);
        }
        NotifyStateChanged();
    }

    /// <summary>
    /// Secret wall interaction + slide animation during editor simulation.
    /// </summary>
    public bool UpdateSecretsDuringSimulation(float deltaTime, bool interactPressed)
    {
        var input = new InputState { IsInteractPressed = interactPressed };
        return SecretSystem.Update(deltaTime, input, Player);
    }

    /// <summary>
    /// Door interaction + animation during editor simulation (same as in-game <see cref="DoorSystem.Update"/>).
    /// </summary>
    public void UpdateDoorsDuringSimulation(float deltaTime, bool interactPressed)
    {
        var input = new InputState { IsInteractPressed = interactPressed };
        UpdateDoors(deltaTime, input);
    }

    /// <summary>Runs secret then door interact with the same priority as play mode.</summary>
    public void UpdateInteractablesDuringSimulation(float deltaTime, bool interactPressed)
    {
        var input = new InputState { IsInteractPressed = interactPressed };
        bool secretConsumed = SecretSystem.Update(deltaTime, input, Player);
        var doorInput = secretConsumed ? input.WithoutInteract() : input;
        UpdateDoors(deltaTime, doorInput);
    }

    private void UpdateDoors(float deltaTime, InputState doorInput)
    {
        _doorOccupancyOthers.Clear();
        var enemies = EnemySystem.Enemies;
        for (int i = 0; i < enemies.Count; i++)
            _doorOccupancyOthers.Add(enemies[i].Position);

        _doorOccupancy.BeginFrame(Player.Position, _doorOccupancyOthers);
        DoorSystem.Update(deltaTime, doorInput, Player.Position, Player, _doorOccupancy);
    }

    public void ClearLevel()
    {
        ExecuteMapMutation(ClearLevelCore, "New level");
        LevelFilename = Path.GetFileName(LevelCatalog.DefaultLevelPath);
        SetStatus("New empty level created");
    }

    private void ClearLevelCore()
    {
        int tileCount = MapData.Width * MapData.Height;
        MapData.Floor = new uint[tileCount];
        MapData.Walls = new uint[tileCount];
        MapData.Ceiling = new uint[tileCount];
        MapData.Doors = new uint[tileCount];
        MapData.Objects = new uint[tileCount];
        MapData.Enemies.Clear();
        MapData.Pickups.Clear();
        MapData.SecretWalls.Clear();
        MapData.Spawn = new PlayerSpawnPlacement();
        DeselectPlayer();
        EnemyTool.ClearSelectionAndHover();
        PickupTool.ClearSelectionAndHover();
        ClearWallSelection();
    }

    public void RefreshLayerReferences()
    {
        EnemyTool.ClearSelectionAndHover();
        PickupTool.ClearSelectionAndHover();
        ClearWallSelection();
        DeselectPlayer();

        foreach (var layer in Layers)
        {
            layer.Tiles = layer.Name switch
            {
                "Floor" => MapData.Floor,
                "Walls" => MapData.Walls,
                "Ceiling" => MapData.Ceiling,
                "Doors" => MapData.Doors,
                ObjectsLayerName => MapData.Objects,
                _ => layer.Tiles
            };
        }
        ApplyPlayerSpawnFromMap();
        RebuildRoomMap();
        NotifyStateChanged();
    }

    public void RebuildRoomMap()
    {
        RoomMap = LevelRoomMap.Build(MapData, DoorTileEncoding.ForEngine);
    }

    public void SetStatus(string message, float duration = 4f)
    {
        StatusMessage = message;
        StatusTimer = duration;
    }

    public void SetLevelFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return;

        LevelFilename = filename;
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
                NotifyStateChanged();
                break;
            }
        }
    }

    public void SwitchToPickupLayer()
    {
        for (int i = 0; i < Layers.Count; i++)
        {
            if (Layers[i].Name == PickupsLayerName)
            {
                ActiveLayerIndex = i;
                NotifyStateChanged();
                break;
            }
        }
    }

    public void SwitchToDoorLayer()
    {
        for (int i = 0; i < Layers.Count; i++)
        {
            if (Layers[i].Name == DoorsLayerName)
            {
                ActiveLayerIndex = i;
                NotifyStateChanged();
                break;
            }
        }
    }

    public void SwitchToObjectLayer()
    {
        for (int i = 0; i < Layers.Count; i++)
        {
            if (Layers[i].Name == ObjectsLayerName)
            {
                ActiveLayerIndex = i;
                if (SelectedTileId != 0 && !ObjectSprites.IsValidObjectId(SelectedTileId))
                    SelectedTileId = 1;
                NotifyStateChanged();
                break;
            }
        }
    }

    public void SwitchToWallsLayer()
    {
        for (int i = 0; i < Layers.Count; i++)
        {
            if (Layers[i].Name == WallsLayerName)
            {
                ActiveLayerIndex = i;
                NotifyStateChanged();
                break;
            }
        }
    }

    public uint GetDoorTileAt(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= MapData.Width || tileY < 0 || tileY >= MapData.Height)
            return 0;
        uint tile = MapData.Doors[MapData.Width * tileY + tileX];
        return DoorTileEncoding.IsDoorTile(tile) ? tile : 0;
    }

    public uint GetWallTileAt(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= MapData.Width || tileY < 0 || tileY >= MapData.Height)
            return 0;
        uint tile = MapData.Walls[MapData.Width * tileY + tileX];
        return tile > 0 ? tile : 0;
    }

    public uint GetObjectTileAt(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= MapData.Width || tileY < 0 || tileY >= MapData.Height)
            return 0;
        uint tile = MapData.Objects[MapData.Width * tileY + tileX];
        return ObjectSprites.IsValidObjectId(tile) ? tile : 0;
    }

    public bool CanPlaceObjectAt(int tileX, int tileY) =>
        GetDoorTileAt(tileX, tileY) == 0 && GetWallTileAt(tileX, tileY) == 0;

    public bool CanPlacePickupAt(int tileX, int tileY) =>
        CanPlaceObjectAt(tileX, tileY) && GetObjectTileAt(tileX, tileY) == 0;

    /// <summary>
    /// When the pickups layer is active, clicking a door or wall tile switches to that layer.
    /// Returns true if the click was handled (layer switched).
    /// </summary>
    public void ApplyPlayerSpawnFromMap() => PlayerSpawnTool.ApplyFromMap();

    public void SyncPlayerToSpawnTile(int tileX, int tileY) =>
        PlayerSpawnTool.SyncToSpawnTile(tileX, tileY);

    public void SetPlayerSpawnRotationIndex(int rotIndex) =>
        PlayerSpawnTool.SetRotationIndex(rotIndex);

    public void ApplyPlayerSpawnRotation() => PlayerSpawnTool.ApplyRotation();

    public static int GetSpawnRotationIndex(float rotationRadians) =>
        PlayerSpawnEditorTool.GetRotationIndex(rotationRadians);

    public void UpdatePlayerHover(EditorCamera camera, Vector2 mouseScreen, bool isMouseOverUI) =>
        PlayerSpawnTool.UpdateHover(camera, mouseScreen, isMouseOverUI);

    public bool TrySwitchLayerFromPickupClick(int tileX, int tileY)
    {
        uint doorTile = GetDoorTileAt(tileX, tileY);
        if (doorTile != 0)
        {
            SwitchToDoorLayer();
            SelectedTileId = doorTile;
            return true;
        }

        uint wallTile = GetWallTileAt(tileX, tileY);
        if (wallTile != 0)
        {
            SwitchToWallsLayer();
            SelectedTileId = wallTile;
            return true;
        }

        return false;
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
