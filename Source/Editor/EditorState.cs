using System.Numerics;
using Game.Engine.Rendering;
using Game.Features.Doors;
using Game.Features.Enemies;
using Game.Features.LevelProgress;
using Game.Features.Pickups;
using Game.Features.Players;
using Game.Features.SoundPropagation;

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

    public enum EditorToolMode { Paint, Select }

    // Tile painting / wall selection
    public int ActiveLayerIndex;
    public uint SelectedTileId = 1;
    public EditorToolMode ToolMode = EditorToolMode.Paint;
    public int SelectedWallTileX = -1;
    public int SelectedWallTileY = -1;

    // Cursor info
    public bool CursorInfoFollowsMouse;

    // Enemy editing
    public int HoveredEnemyIndex = -1;
    public int SelectedEnemyIndex = -1;
    public bool IsDraggingEnemy;

    // Pickup editing
    public PickupType SelectedPickupType = PickupType.Health;
    public int HoveredPickupIndex = -1;
    public int SelectedPickupIndex = -1;
    public bool IsDraggingPickup;

    // Player spawn editing
    public bool HoveredPlayer;
    public bool IsPlayerSelected;
    public bool IsDraggingPlayer;

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

    // Sound propagation visualizer
    public bool SoundPropagationPicking;
    public List<Vector2>? SoundPropagationTiles;
    public float SoundPropagationShowUntil;
    public const float SoundPropagationDurationSeconds = 2f;

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

    // Status message
    public string StatusMessage = "";
    public float StatusTimer;

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
    public bool IsOnEnemyLayer => Layers[ActiveLayerIndex].Name == EnemiesLayerName;
    public bool IsOnPickupLayer => Layers[ActiveLayerIndex].Name == PickupsLayerName;
    public bool IsOnDoorLayer => Layers[ActiveLayerIndex].Name == DoorsLayerName;
    public bool IsOnObjectLayer => Layers[ActiveLayerIndex].Name == ObjectsLayerName;
    public bool IsOnWallsLayer => Layers[ActiveLayerIndex].Name == WallsLayerName;
    public bool IsOnTileLayer => !IsOnEnemyLayer && !IsOnPickupLayer;
    public bool HasSelectedWall => SelectedWallTileX >= 0 && SelectedWallTileY >= 0;
    public bool IsWallSelectMode => ToolMode == EditorToolMode.Select && IsOnWallsLayer;

    public const string DoorsLayerName = "Doors";

    public void NotifyStateChanged() => StateChanged?.Invoke();

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
            ClearWallSelection();
        SetStatus(mode == EditorToolMode.Paint ? "Paint mode" : "Select mode (Walls layer)");
        NotifyStateChanged();
    }

    public void SelectWallTile(int tileX, int tileY)
    {
        if (GetWallTileAt(tileX, tileY) == 0) return;

        DeselectEnemy();
        DeselectPickup();
        DeselectPlayer();
        SelectedWallTileX = tileX;
        SelectedWallTileY = tileY;
        NotifyStateChanged();
    }

    public void ClearWallSelection()
    {
        if (SelectedWallTileX < 0) return;
        SelectedWallTileX = -1;
        SelectedWallTileY = -1;
        NotifyStateChanged();
    }

    public SecretWallPlacement? FindSecretWallAt(int tileX, int tileY) =>
        MapData.SecretWalls.FirstOrDefault(s => s.TileX == tileX && s.TileY == tileY);

    public SecretWallPlacement? GetSelectedSecretPlacement() =>
        HasSelectedWall ? FindSecretWallAt(SelectedWallTileX, SelectedWallTileY) : null;

    public bool IsSelectedWallSecret => GetSelectedSecretPlacement() != null;

    public void SetWallSecret(bool isSecret, SecretWallDirection direction, int travelTiles)
    {
        if (!HasSelectedWall) return;

        RemoveSecretWallAt(SelectedWallTileX, SelectedWallTileY);
        if (isSecret)
        {
            MapData.SecretWalls.Add(new SecretWallPlacement
            {
                TileX = SelectedWallTileX,
                TileY = SelectedWallTileY,
                Direction = direction,
                TravelTiles = ClampSecretTravelTiles(SelectedWallTileX, SelectedWallTileY, direction, travelTiles)
            });
        }

        RebuildRoomMap();
        NotifyStateChanged();
    }

    public void RemoveSecretWallAt(int tileX, int tileY)
    {
        int index = MapData.SecretWalls.FindIndex(s => s.TileX == tileX && s.TileY == tileY);
        if (index >= 0)
            MapData.SecretWalls.RemoveAt(index);
    }

    public int GetMaxSecretTravelTiles(int tileX, int tileY, SecretWallDirection direction)
    {
        var (dx, dy) = SecretWallDirectionHelper.ToTileDelta(direction);
        int count = 0;
        int x = tileX + dx;
        int y = tileY + dy;
        while (x >= 0 && x < MapData.Width && y >= 0 && y < MapData.Height)
        {
            count++;
            x += dx;
            y += dy;
        }

        return Math.Max(1, count);
    }

    public int ClampSecretTravelTiles(int tileX, int tileY, SecretWallDirection direction, int travelTiles) =>
        Math.Clamp(travelTiles, 1, GetMaxSecretTravelTiles(tileX, tileY, direction));

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

        var layer = Layers[ActiveLayerIndex];
        layer.Tiles[MapData.Width * y + x] = SelectedTileId;

        if (IsOnWallsLayer && SelectedTileId == 0)
            RemoveSecretWallAt(x, y);

        if (!IsOnEnemyLayer && !IsOnPickupLayer && !IsOnObjectLayer)
            RebuildRoomMap();
    }

    public void PlaceEnemy(int x, int y)
    {
        if (x < 0 || x >= MapData.Width || y < 0 || y >= MapData.Height) return;
        MapData.Enemies.Add(new EnemyPlacement
        {
            TileX = x, TileY = y, Rotation = 0, EnemyType = "Guard"
        });
        DeselectPickup();
        DeselectPlayer();
        SelectedEnemyIndex = MapData.Enemies.Count - 1;
        IsDraggingEnemy = true;
        NotifyStateChanged();
    }

    public void DeselectEnemy()
    {
        SelectedEnemyIndex = -1;
        IsDraggingEnemy = false;
    }

    public void DeselectPickup()
    {
        SelectedPickupIndex = -1;
        IsDraggingPickup = false;
    }

    public void DeselectPlayer()
    {
        IsPlayerSelected = false;
        IsDraggingPlayer = false;
    }

    public void SelectPlayer()
    {
        DeselectEnemy();
        DeselectPickup();
        IsPlayerSelected = true;
        IsDraggingPlayer = true;
        NotifyStateChanged();
    }

    /// <summary>Hide the yellow tile-under-cursor highlight when pointing at or dragging an entity.</summary>
    public bool ShouldShowTileHighlight() =>
        !HoveredPlayer && HoveredEnemyIndex < 0 && HoveredPickupIndex < 0
        && !IsDraggingPlayer && !IsDraggingEnemy && !IsDraggingPickup;

    public void SelectEnemy(int index)
    {
        DeselectPickup();
        DeselectPlayer();
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

    public int FindPickupIndexAt(int tileX, int tileY) =>
        MapData.Pickups.FindIndex(p => p.TileX == tileX && p.TileY == tileY);

    public void PlacePickup(int x, int y)
    {
        if (x < 0 || x >= MapData.Width || y < 0 || y >= MapData.Height) return;
        if (!CanPlacePickupAt(x, y))
        {
            SetStatus("Cannot place pickup on walls or doors");
            return;
        }

        int existing = FindPickupIndexAt(x, y);
        if (existing >= 0)
            MapData.Pickups.RemoveAt(existing);

        MapData.Pickups.Add(new PickupPlacement
        {
            TileX = x,
            TileY = y,
            Type = SelectedPickupType
        });
        DeselectEnemy();
        DeselectPlayer();
        SelectedPickupIndex = MapData.Pickups.Count - 1;
        IsDraggingPickup = true;
        SetStatus($"Placed {SelectedPickupType} pickup");
        NotifyStateChanged();
    }

    public void SelectPickup(int index)
    {
        DeselectEnemy();
        DeselectPlayer();
        SelectedPickupIndex = index;
        IsDraggingPickup = true;
        if (index >= 0 && index < MapData.Pickups.Count)
            SelectedPickupType = MapData.Pickups[index].Type;
        NotifyStateChanged();
    }

    public void MovePickup(int x, int y)
    {
        if (SelectedPickupIndex < 0 || SelectedPickupIndex >= MapData.Pickups.Count) return;
        if (x < 0 || x >= MapData.Width || y < 0 || y >= MapData.Height) return;
        if (!CanPlacePickupAt(x, y)) return;

        var pickup = MapData.Pickups[SelectedPickupIndex];
        if (pickup.TileX == x && pickup.TileY == y)
            return;

        int occupant = FindPickupIndexAt(x, y);
        if (occupant >= 0 && occupant != SelectedPickupIndex)
            MapData.Pickups.RemoveAt(occupant);

        pickup.TileX = x;
        pickup.TileY = y;
        if (SelectedPickupIndex >= MapData.Pickups.Count)
            SelectedPickupIndex = MapData.Pickups.Count - 1;
    }

    public void DeleteSelectedPickup()
    {
        if (SelectedPickupIndex < 0 || SelectedPickupIndex >= MapData.Pickups.Count) return;
        MapData.Pickups.RemoveAt(SelectedPickupIndex);
        SelectedPickupIndex = -1;
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
        DoorSystem.Update(deltaTime, input, Player, EnemySystem.Enemies);
    }

    /// <summary>Runs secret then door interact with the same priority as play mode.</summary>
    public void UpdateInteractablesDuringSimulation(float deltaTime, bool interactPressed)
    {
        var input = new InputState { IsInteractPressed = interactPressed };
        bool secretConsumed = SecretSystem.Update(deltaTime, input, Player);
        var doorInput = secretConsumed ? input.WithoutInteract() : input;
        DoorSystem.Update(deltaTime, doorInput, Player, EnemySystem.Enemies);
    }

    public void ClearLevel()
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
        SelectedEnemyIndex = -1;
        HoveredEnemyIndex = -1;
        SelectedPickupIndex = -1;
        HoveredPickupIndex = -1;
        ClearWallSelection();
        RefreshLayerReferences();
        SetStatus("New empty level created");
    }

    public void RefreshLayerReferences()
    {
        SelectedEnemyIndex = -1;
        HoveredEnemyIndex = -1;
        SelectedPickupIndex = -1;
        HoveredPickupIndex = -1;
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
        RoomMap = LevelRoomMap.Build(MapData);
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
    public void ApplyPlayerSpawnFromMap()
    {
        PlayerSpawn.ApplyFromMap(Player, MapData, PlayerSpawnApplyMode.PositionAndCameraOnly);
        NotifyStateChanged();
    }

    public void SyncPlayerToSpawnTile(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= MapData.Width || tileY < 0 || tileY >= MapData.Height)
            return;

        MapData.Spawn.TileX = tileX;
        MapData.Spawn.TileY = tileY;
        PlayerSpawn.ApplyFromMap(Player, MapData, PlayerSpawnApplyMode.PositionAndCameraOnly);
        NotifyStateChanged();
    }

    public void SetPlayerSpawnRotationIndex(int rotIndex)
    {
        const float step = MathF.PI / 4f;
        MapData.Spawn.Rotation = Math.Clamp(rotIndex, 0, 7) * step;
        PlayerSpawn.ApplyCameraFromMap(Player, MapData);
        NotifyStateChanged();
    }

    public void ApplyPlayerSpawnRotation() =>
        PlayerSpawn.ApplyCameraFromMap(Player, MapData);

    public static int GetSpawnRotationIndex(float rotationRadians)
    {
        const float step = MathF.PI / 4f;
        return Math.Clamp((int)MathF.Round(rotationRadians / step), 0, 7);
    }

    public void UpdatePlayerHover(EditorCamera camera, Vector2 mouseScreen, bool isMouseOverUI)
    {
        HoveredPlayer = false;
        if (isMouseOverUI) return;

        float tileSize = camera.TileSize;
        float quadSize = LevelData.QuadSize;
        float tileX = Player.Position.X / quadSize;
        float tileY = Player.Position.Z / quadSize;
        float centerX = (tileX + 0.5f) * tileSize + camera.Offset.X;
        float centerY = (tileY + 0.5f) * tileSize + camera.Offset.Y;
        float radius = tileSize * 0.35f;
        float dx = mouseScreen.X - centerX;
        float dy = mouseScreen.Y - centerY;
        HoveredPlayer = dx * dx + dy * dy <= radius * radius;
    }

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

    // ─── Pathfinding visualizer ──────────────────────────────────────────────────

    public void StartPickingPathStart()
    {
        SoundPropagationPicking = false;
        PathPickingMode = PathPickMode.Start;
        NotifyStateChanged();
    }

    public void StartPickingPathEnd()
    {
        SoundPropagationPicking = false;
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

    // ─── Sound propagation visualizer ────────────────────────────────────────────

    public void StartSoundPropagationPick()
    {
        PathPickingMode = PathPickMode.None;
        SoundPropagationPicking = true;
        NotifyStateChanged();
    }

    public void CancelSoundPropagationPick()
    {
        if (!SoundPropagationPicking) return;
        SoundPropagationPicking = false;
        NotifyStateChanged();
    }

    public void RunSoundPropagationTest(int tileX, int tileY, float now)
    {
        SoundPropagationPicking = false;

        if (tileX < 0 || tileX >= MapData.Width || tileY < 0 || tileY >= MapData.Height)
            return;

        bool treatAllDoorsClosed = !IsSimulating;
        var reach = SoundPropagation.ComputeReach(
            MapData, DoorSystem.Doors, tileX, tileY, treatAllDoorsClosed);

        if (reach.Count == 0)
        {
            SoundPropagationTiles = null;
            SoundPropagationShowUntil = 0;
            SetStatus("Sound propagation: invalid origin (wall or closed door)");
            NotifyStateChanged();
            return;
        }

        SoundPropagationTiles = reach
            .Select(t => new Vector2(t.X, t.Y))
            .ToList();
        SoundPropagationShowUntil = now + SoundPropagationDurationSeconds;
        SetStatus($"Sound reached {reach.Count} tiles");
        NotifyStateChanged();
    }

    public void TickSoundPropagationOverlay(float now)
    {
        if (SoundPropagationTiles == null || SoundPropagationShowUntil <= 0)
            return;

        if (now >= SoundPropagationShowUntil)
        {
            SoundPropagationTiles = null;
            SoundPropagationShowUntil = 0;
            NotifyStateChanged();
        }
    }

    public void ClearSoundPropagation()
    {
        SoundPropagationPicking = false;
        SoundPropagationTiles = null;
        SoundPropagationShowUntil = 0;
        NotifyStateChanged();
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
