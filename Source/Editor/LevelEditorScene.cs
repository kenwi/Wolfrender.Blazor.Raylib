using System.Numerics;
using Game.Systems;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using static Raylib_cs.Raylib;
using Color = Raylib_cs.Color;

namespace Game.Editor;

public class LevelEditorScene : IScene
{
    private readonly MapData _mapData;
    private readonly List<EditorLayer> _layers;

    // Sub-systems
    private readonly EditorCamera _camera;
    private readonly EditorMapRenderer _mapRenderer;
    private readonly EditorGui _gui;

    // Cursor info panel
    private bool _cursorInfoFollowsMouse;

    // Tile painting
    private int _activeLayerIndex;
    private uint _selectedTileId = 1;

    // Enemy editing
    private const string EnemiesLayerName = "Enemies";
    private int _hoveredEnemyIndex = -1;
    private int _selectedEnemyIndex = -1;
    private bool _isDraggingEnemy;

    // Patrol path editing
    private bool _isEditingPatrolPath;
    private int _patrolEditEnemyIndex = -1;
    private List<PatrolWaypoint> _patrolPathInProgress = new();

    // Simulation
    private readonly EnemySystem _enemySystem;
    private readonly DoorSystem _doorSystem;
    private readonly Entities.Player _player;
    private bool _isSimulating;

    public LevelEditorScene(MapData mapData, EnemySystem enemySystem, DoorSystem doorSystem, Entities.Player player)
    {
        _mapData = mapData;
        _enemySystem = enemySystem;
        _doorSystem = doorSystem;
        _player = player;

        _camera = new EditorCamera();
        _camera.CenterOnMap(mapData.Width, mapData.Height);

        _mapRenderer = new EditorMapRenderer(mapData);
        _gui = new EditorGui(mapData);

        _layers = new List<EditorLayer>
        {
            new() { Name = "Floor", Tiles = mapData.Floor },
            new() { Name = "Walls", Tiles = mapData.Walls },
            new() { Name = "Ceiling", Tiles = mapData.Ceiling, IsVisible = false },
            new() { Name = "Doors", Tiles = mapData.Doors },
            new() { Name = EnemiesLayerName, Tiles = Array.Empty<uint>() },
        };
    }

    public void OnEnter()
    {
        ShowCursor();
    }

    public void OnExit()
    {
        _isSimulating = false;
    }

    public void Update(float deltaTime)
    {
        // Toggle cursor info follow mode
        if (IsKeyPressed(KeyboardKey.C))
        {
            _cursorInfoFollowsMouse = !_cursorInfoFollowsMouse;
        }

        // Layer hotkeys: 1-9 to activate, Ctrl+1-9 to toggle visibility
        bool ctrlHeld = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);
        KeyboardKey[] numKeys = {
            KeyboardKey.One, KeyboardKey.Two, KeyboardKey.Three,
            KeyboardKey.Four, KeyboardKey.Five, KeyboardKey.Six,
            KeyboardKey.Seven, KeyboardKey.Eight, KeyboardKey.Nine
        };
        for (int i = 0; i < numKeys.Length && i < _layers.Count; i++)
        {
            if (IsKeyPressed(numKeys[i]))
            {
                if (ctrlHeld)
                    _layers[i].IsVisible = !_layers[i].IsVisible;
                else
                    _activeLayerIndex = i;
            }
        }

        // Status message and GUI scaling
        _gui.UpdateStatusTimer(deltaTime);
        _gui.HandleScalingInput();

        // Toggle simulation with P key
        if (IsKeyPressed(KeyboardKey.P))
        {
            ToggleSimulation();
        }

        // Tick game systems when simulating
        if (_isSimulating)
        {
            UpdatePlayerMovement(deltaTime);
            _enemySystem.Update(deltaTime);
            _doorSystem.Animate(deltaTime);
        }

        bool imGuiWantsMouse = ImGui.GetIO().WantCaptureMouse;

        // Camera input (pan + zoom) — disable WASD panning during simulation (player uses those keys)
        _camera.HandleInput(deltaTime, ctrlHeld, disableKeyboardPan: _isSimulating);

        // Patrol path editing mode
        if (_isEditingPatrolPath)
        {
            HandlePatrolPathInput(imGuiWantsMouse);
        }
        else
        {
            HandleTileAndEnemyInput(imGuiWantsMouse);
        }
    }

    public void Render()
    {
        BeginDrawing();
        ClearBackground(new Color(40, 40, 40, 255));

        // Draw grid background
        _mapRenderer.DrawMapGrid(_camera);

        // Render tile layers in order
        for (int i = 0; i < _layers.Count; i++)
        {
            var layer = _layers[i];
            if (!layer.IsVisible) continue;

            if (layer.Name == EnemiesLayerName)
            {
                _mapRenderer.RenderEnemyLayer(
                    _camera, _enemySystem, _isSimulating,
                    ref _hoveredEnemyIndex, _selectedEnemyIndex,
                    _isEditingPatrolPath, _patrolEditEnemyIndex, _patrolPathInProgress);
            }
            else if (_isSimulating && layer.Name == "Doors")
            {
                _mapRenderer.RenderLiveDoors(_doorSystem, _camera);
            }
            else
            {
                _mapRenderer.RenderLayer(layer, _camera);
            }
        }

        // Draw player position indicator
        _mapRenderer.RenderPlayerIndicator(_player, _camera);

        // Highlight hovered tile
        var mouseScreen = GetMousePosition();
        var worldPos = _camera.ScreenToWorld(mouseScreen);
        int tileX = (int)MathF.Floor(worldPos.X);
        int tileY = (int)MathF.Floor(worldPos.Y);
        bool tileInBounds = tileX >= 0 && tileX < _mapData.Width && tileY >= 0 && tileY < _mapData.Height;

        if (tileInBounds)
        {
            _mapRenderer.DrawTileHighlight(tileX, tileY, _camera);
        }

        // ImGui panels
        rlImGui.Begin();
        bool menuToggleSim = _gui.RenderMenuBar(_isSimulating, _enemySystem, _doorSystem, ClearLevel, RefreshLayerReferences);
        if (menuToggleSim) ToggleSimulation();
        _gui.RenderFileDialogs(RefreshLayerReferences);
        _gui.RenderLayerPanel(_layers, ref _activeLayerIndex);
        _gui.RenderTilePalette(_layers, _activeLayerIndex, ref _selectedTileId);
        _gui.RenderInfoPanel(tileX, tileY, worldPos, tileInBounds, _cursorInfoFollowsMouse, _layers, EnemiesLayerName);
        _gui.RenderEnemyPropertiesPanel(ref _selectedEnemyIndex, ref _isEditingPatrolPath, ref _patrolEditEnemyIndex, _patrolPathInProgress);
        _gui.RenderDebugLogPanel();
        rlImGui.End();

        DrawText("Level Editor - F1 to return to game", 10, GetScreenHeight() - 70, 20, Color.White);
        DrawText($"Zoom: {_camera.Zoom:F2}x", 10, GetScreenHeight() - 45, 20, Color.LightGray);

        if (_isEditingPatrolPath)
        {
            const string msg = "EDITING PATROL PATH - LMB: Add waypoint | Enter: Confirm | Esc: Cancel";
            int msgW = MeasureText(msg, 24);
            DrawText(msg, (GetScreenWidth() - msgW) / 2, GetScreenHeight() - 100, 24, Color.Yellow);
        }

        _gui.DrawStatusMessage();

        EndDrawing();
    }

    // ─── Input Helpers ───────────────────────────────────────────────────────────

    private void HandlePatrolPathInput(bool imGuiWantsMouse)
    {
        if (!imGuiWantsMouse && IsMouseButtonPressed(MouseButton.Left))
        {
            var paintPos = _camera.ScreenToWorld(GetMousePosition());
            int px = (int)MathF.Floor(paintPos.X);
            int py = (int)MathF.Floor(paintPos.Y);
            if (px >= 0 && px < _mapData.Width && py >= 0 && py < _mapData.Height)
            {
                _patrolPathInProgress.Add(new PatrolWaypoint { TileX = px, TileY = py });
            }
        }

        if (IsKeyPressed(KeyboardKey.Enter) || IsKeyPressed(KeyboardKey.KpEnter))
        {
            if (_patrolEditEnemyIndex >= 0 && _patrolEditEnemyIndex < _mapData.Enemies.Count)
            {
                _mapData.Enemies[_patrolEditEnemyIndex].PatrolPath = new List<PatrolWaypoint>(_patrolPathInProgress);
            }
            _isEditingPatrolPath = false;
            _patrolPathInProgress.Clear();
            _patrolEditEnemyIndex = -1;
        }

        if (IsKeyPressed(KeyboardKey.Escape))
        {
            _isEditingPatrolPath = false;
            _patrolPathInProgress.Clear();
            _patrolEditEnemyIndex = -1;
        }
    }

    private void HandleTileAndEnemyInput(bool imGuiWantsMouse)
    {
        bool isEnemyLayer = _layers[_activeLayerIndex].Name == EnemiesLayerName;

        // If clicking on a hovered enemy while another layer is active, auto-switch to enemy layer
        if (!imGuiWantsMouse && !isEnemyLayer && _hoveredEnemyIndex >= 0 && IsMouseButtonPressed(MouseButton.Left))
        {
            for (int li = 0; li < _layers.Count; li++)
            {
                if (_layers[li].Name == EnemiesLayerName)
                {
                    _activeLayerIndex = li;
                    break;
                }
            }
            isEnemyLayer = true;
            _selectedEnemyIndex = _hoveredEnemyIndex;
            _isDraggingEnemy = true;
        }

        if (!imGuiWantsMouse && isEnemyLayer)
        {
            HandleEnemyInput(imGuiWantsMouse);
        }
        else if (!imGuiWantsMouse && IsMouseButtonDown(MouseButton.Left) && !isEnemyLayer)
        {
            // Tile layer: paint tiles (click or drag)
            var paintPos = _camera.ScreenToWorld(GetMousePosition());
            int px = (int)MathF.Floor(paintPos.X);
            int py = (int)MathF.Floor(paintPos.Y);
            if (px >= 0 && px < _mapData.Width && py >= 0 && py < _mapData.Height)
            {
                var activeLayer = _layers[_activeLayerIndex];
                int index = _mapData.Width * py + px;
                activeLayer.Tiles[index] = _selectedTileId;
            }
        }

        // Delete selected enemy with Delete key
        if (isEnemyLayer && _selectedEnemyIndex >= 0 && _selectedEnemyIndex < _mapData.Enemies.Count
            && IsKeyPressed(KeyboardKey.Delete))
        {
            _mapData.Enemies.RemoveAt(_selectedEnemyIndex);
            _selectedEnemyIndex = -1;
        }
    }

    private void HandleEnemyInput(bool imGuiWantsMouse)
    {
        if (IsMouseButtonPressed(MouseButton.Left))
        {
            if (_hoveredEnemyIndex >= 0)
            {
                _selectedEnemyIndex = _hoveredEnemyIndex;
                _isDraggingEnemy = true;
            }
            else
            {
                var paintPos = _camera.ScreenToWorld(GetMousePosition());
                int px = (int)MathF.Floor(paintPos.X);
                int py = (int)MathF.Floor(paintPos.Y);
                if (px >= 0 && px < _mapData.Width && py >= 0 && py < _mapData.Height)
                {
                    _mapData.Enemies.Add(new EnemyPlacement
                    {
                        TileX = px,
                        TileY = py,
                        Rotation = 0,
                        EnemyType = "Guard"
                    });
                    _selectedEnemyIndex = _mapData.Enemies.Count - 1;
                    _isDraggingEnemy = true;
                }
            }
        }

        if (_isDraggingEnemy && IsMouseButtonDown(MouseButton.Left)
            && _selectedEnemyIndex >= 0 && _selectedEnemyIndex < _mapData.Enemies.Count)
        {
            var dragPos = _camera.ScreenToWorld(GetMousePosition());
            int dx = (int)MathF.Floor(dragPos.X);
            int dy = (int)MathF.Floor(dragPos.Y);
            if (dx >= 0 && dx < _mapData.Width && dy >= 0 && dy < _mapData.Height)
            {
                _mapData.Enemies[_selectedEnemyIndex].TileX = dx;
                _mapData.Enemies[_selectedEnemyIndex].TileY = dy;
            }
        }

        if (IsMouseButtonReleased(MouseButton.Left))
        {
            _isDraggingEnemy = false;
        }
    }

    // ─── Simulation Helpers ─────────────────────────────────────────────────────

    private void UpdatePlayerMovement(float deltaTime)
    {
        const float rotationSpeed = 2.5f; // radians per second

        var camera = _player.Camera;
        Vector3 forward = Vector3.Normalize(camera.Target - camera.Position);

        // Arrow Left / Right to rotate the player
        float yawDelta = 0;
        if (IsKeyDown(KeyboardKey.Left))  yawDelta += rotationSpeed * deltaTime;
        if (IsKeyDown(KeyboardKey.Right)) yawDelta -= rotationSpeed * deltaTime;

        if (MathF.Abs(yawDelta) > 0.0001f)
        {
            var rotMatrix = Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, yawDelta);
            forward = Vector3.Transform(forward, rotMatrix);
        }

        // Project forward onto the horizontal (XZ) plane for movement
        Vector3 forwardXZ = new Vector3(forward.X, 0, forward.Z);
        float forwardLen = forwardXZ.Length();
        if (forwardLen > 0.001f)
            forwardXZ /= forwardLen;
        else
            forwardXZ = Vector3.UnitZ;

        // Right vector (same convention as InputSystem.GetMoveDirection)
        Vector3 right = Vector3.Cross(forwardXZ, -Vector3.UnitY);
        float rightLen = right.Length();
        if (rightLen > 0.001f)
            right /= rightLen;

        // WASD to move
        Vector3 moveDir = Vector3.Zero;
        if (IsKeyDown(KeyboardKey.W)) moveDir += forwardXZ;
        if (IsKeyDown(KeyboardKey.S)) moveDir -= forwardXZ;
        if (IsKeyDown(KeyboardKey.A)) moveDir += right;
        if (IsKeyDown(KeyboardKey.D)) moveDir -= right;

        float moveDirLen = moveDir.Length();
        if (moveDirLen > 0.001f)
        {
            moveDir /= moveDirLen;
            _player.Position += moveDir * _player.MoveSpeed * deltaTime;
        }

        // Sync camera with updated position and look direction
        camera.Position = _player.Position;
        camera.Target = camera.Position + forward;
        _player.Camera = camera;
    }

    // ─── Level Management ────────────────────────────────────────────────────────

    private void ToggleSimulation()
    {
        _isSimulating = !_isSimulating;
        if (_isSimulating)
        {
            _enemySystem.Rebuild(_mapData.Enemies, _mapData);
            _doorSystem.Rebuild(_mapData.Doors, _mapData.Width);
        }
    }

    private void ClearLevel()
    {
        int tileCount = _mapData.Width * _mapData.Height;
        _mapData.Floor = new uint[tileCount];
        _mapData.Walls = new uint[tileCount];
        _mapData.Ceiling = new uint[tileCount];
        _mapData.Doors = new uint[tileCount];
        _mapData.Enemies.Clear();
        _selectedEnemyIndex = -1;
        _hoveredEnemyIndex = -1;
        RefreshLayerReferences();
        _gui.SetStatus("New empty level created");
    }

    private void RefreshLayerReferences()
    {
        _selectedEnemyIndex = -1;
        _hoveredEnemyIndex = -1;

        foreach (var layer in _layers)
        {
            layer.Tiles = layer.Name switch
            {
                "Floor" => _mapData.Floor,
                "Walls" => _mapData.Walls,
                "Ceiling" => _mapData.Ceiling,
                "Doors" => _mapData.Doors,
                _ => layer.Tiles
            };
        }
    }
}
