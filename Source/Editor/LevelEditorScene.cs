using System.Numerics;
using Game.Engine.Movement;
using Game.Features.Doors;
using Game.Features.Enemies;
using Game.Features.LevelProgress;
using Game.Features.Players;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using static Raylib_cs.Raylib;
using Color = Raylib_cs.Color;

namespace Game.Editor;

public class LevelEditorScene : IScene
{
    private readonly EditorState _state;
    private readonly EditorMapRenderer _mapRenderer;
    private readonly EditorGui _gui;
    private readonly CollisionSystem _collisionSystem;

    /// <summary>
    /// True while the left mouse button has been "consumed" by a non-map action
    /// (e.g. a path-pick click) and must not paint tiles or drag enemies until released.
    /// </summary>
    private bool _suppressMapClickUntilRelease;

    public LevelEditorScene(MapData mapData, EnemySystem enemySystem, DoorSystem doorSystem, SecretSystem secretSystem, Player player)
    {
        _state = new EditorState(mapData, enemySystem, doorSystem, secretSystem, player);
        _mapRenderer = new EditorMapRenderer(mapData);
        _gui = new EditorGui(mapData);
        _collisionSystem = new CollisionSystem(
            new LevelData(mapData),
            new CompositeMovementBlocker(doorSystem, secretSystem));
    }

    public void OnEnter()
    {
        ShowCursor();
    }

    public void OnExit()
    {
        _state.IsSimulating = false;
    }

    public void Update(float deltaTime)
    {
        // Toggle cursor info follow mode
        if (IsKeyPressed(KeyboardKey.C))
        {
            _state.CursorInfoFollowsMouse = !_state.CursorInfoFollowsMouse;
        }

        // Layer hotkeys: 1-9 to activate, Ctrl+1-9 to toggle visibility
        bool ctrlHeld = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);
        KeyboardKey[] numKeys = {
            KeyboardKey.One, KeyboardKey.Two, KeyboardKey.Three,
            KeyboardKey.Four, KeyboardKey.Five, KeyboardKey.Six,
            KeyboardKey.Seven, KeyboardKey.Eight, KeyboardKey.Nine
        };
        for (int i = 0; i < numKeys.Length && i < _state.Layers.Count; i++)
        {
            if (IsKeyPressed(numKeys[i]))
            {
                if (ctrlHeld)
                    _state.Layers[i].IsVisible = !_state.Layers[i].IsVisible;
                else
                    _state.ActiveLayerIndex = i;
            }
        }

        // Status message and GUI scaling
        _state.UpdateStatusTimer(deltaTime);
        _state.TickSoundPropagationOverlay((float)GetTime());
        _gui.HandleScalingInput();

        // Read ImGui IO before keyboard routing
        bool imGuiWantsMouse = ImGui.GetIO().WantCaptureMouse;
        bool imGuiWantsKeyboard = ImGui.GetIO().WantCaptureKeyboard;

        // Toggle simulation with P key
        if (IsKeyPressed(KeyboardKey.P))
        {
            _state.ToggleSimulation();
        }

        if (!imGuiWantsKeyboard)
        {
            if (IsKeyPressed(KeyboardKey.B))
                _state.SetToolMode(EditorState.EditorToolMode.Paint);
            if (IsKeyPressed(KeyboardKey.V))
                _state.SetToolMode(EditorState.EditorToolMode.Select);
        }

        // Tick game systems when simulating
        if (_state.IsSimulating)
        {
            UpdatePlayerMovement(deltaTime);
            _state.EnemySystem.Update(deltaTime);
            bool interactPressed = !imGuiWantsKeyboard && IsKeyPressed(KeyboardKey.E);
            _state.UpdateInteractablesDuringSimulation(deltaTime, interactPressed);
        }
        _state.IsMouseOverUI = imGuiWantsMouse;

        // Camera input (pan + zoom) - disable WASD panning during simulation (player uses those keys)
        _state.Camera.HandleInput(deltaTime, ctrlHeld, imGuiWantsMouse, imGuiWantsKeyboard, disableKeyboardPan: _state.IsSimulating);

        // Drop the click-suppression latch as soon as the user lets go.
        if (IsMouseButtonReleased(MouseButton.Left))
            _suppressMapClickUntilRelease = false;

        // Patrol path editing mode
        if (_state.IsEditingPatrolPath)
        {
            HandlePatrolPathInput(imGuiWantsMouse);
        }
        else if (_state.PathPickingMode != EditorState.PathPickMode.None)
        {
            HandlePathPickInput(imGuiWantsMouse);
        }
        else if (_state.SoundPropagationPicking)
        {
            HandleSoundPropagationPickInput(imGuiWantsMouse);
        }
        else if (!_suppressMapClickUntilRelease)
        {
            HandleTileAndEnemyInput(imGuiWantsMouse);
        }
    }

    private void HandlePathPickInput(bool mouseOverUI)
    {
        if (IsKeyPressed(KeyboardKey.Escape))
        {
            _state.CancelPathPicking();
            return;
        }

        if (mouseOverUI || !IsMouseButtonPressed(MouseButton.Left)) return;

        var worldPos = _state.Camera.ScreenToWorld(GetMousePosition());
        int tx = (int)MathF.Floor(worldPos.X);
        int ty = (int)MathF.Floor(worldPos.Y);
        _state.SetPathPickPoint(tx, ty);

        // Keep painting/enemy drag suppressed for the rest of this press —
        // otherwise a long-held click would paint a tile on the very next frame
        // since the picker has already switched mode back to None.
        _suppressMapClickUntilRelease = true;
    }

    private void HandleSoundPropagationPickInput(bool mouseOverUI)
    {
        if (IsKeyPressed(KeyboardKey.Escape))
        {
            _state.CancelSoundPropagationPick();
            return;
        }

        if (mouseOverUI || !IsMouseButtonPressed(MouseButton.Left)) return;

        var worldPos = _state.Camera.ScreenToWorld(GetMousePosition());
        int tx = (int)MathF.Floor(worldPos.X);
        int ty = (int)MathF.Floor(worldPos.Y);
        _state.RunSoundPropagationTest(tx, ty, (float)GetTime());

        _suppressMapClickUntilRelease = true;
    }

    public void Render()
    {
        BeginDrawing();
        ClearBackground(new Color(40, 40, 40, 255));

        // Draw grid background
        _mapRenderer.DrawMapGrid(_state.Camera);

        // Render tile layers in order
        for (int i = 0; i < _state.Layers.Count; i++)
        {
            var layer = _state.Layers[i];
            if (!layer.IsVisible) continue;

            if (layer.Name == EditorState.EnemiesLayerName)
            {
                _mapRenderer.RenderEnemyLayer(
                    _state.Camera, _state.EnemySystem, _state.IsMouseOverUI,
                    _state.IsSimulating, _state.DrawEnemyLineOfSight, _state.ShowPatrolPaths,
                    ref _state.HoveredEnemyIndex, _state.SelectedEnemyIndex,
                    _state.IsEditingPatrolPath, _state.PatrolEditEnemyIndex, _state.PatrolPathInProgress);
            }
            else if (layer.Name == EditorState.PickupsLayerName)
            {
                _mapRenderer.RenderPickupLayer(
                    _state.Camera, _state.IsMouseOverUI,
                    ref _state.HoveredPickupIndex, _state.SelectedPickupIndex);
            }
            else if (layer.Name == EditorState.ObjectsLayerName)
            {
                _mapRenderer.RenderObjectLayer(_state.Camera);
            }
            else if (_state.IsSimulating && layer.Name == EditorState.DoorsLayerName)
            {
                _mapRenderer.RenderLiveDoors(_state.DoorSystem, _state.Camera);
            }
            else
            {
                _mapRenderer.RenderLayer(layer, _state.Camera);
            }
        }

        // Draw player position indicator
        _mapRenderer.RenderPlayerIndicator(
            _state.Player, _state.Camera, _state.MapData.Spawn.Rotation,
            _state.HoveredPlayer, _state.IsPlayerSelected, _state.IsDraggingPlayer);

        // Pathfinding visualizer overlay
        _mapRenderer.DrawPathPreview(_state.PathStart, _state.PathEnd, _state.PathResult, _state.Camera);

        _mapRenderer.DrawSoundPropagationOverlay(_state.SoundPropagationTiles, _state.Camera);

        if (_state.IsSimulating && _state.DrawEnemyPaths)
            _mapRenderer.DrawEnemyChasePaths(_state.EnemySystem, _state.Camera);

        // Highlight hovered tile
        var mouseScreen = GetMousePosition();
        var worldPos = _state.Camera.ScreenToWorld(mouseScreen);
        int tileX = (int)MathF.Floor(worldPos.X);
        int tileY = (int)MathF.Floor(worldPos.Y);
        bool tileInBounds = tileX >= 0 && tileX < _state.MapData.Width && tileY >= 0 && tileY < _state.MapData.Height;

        if (tileInBounds && _state.ShouldShowTileHighlight())
        {
            _mapRenderer.DrawTileHighlight(tileX, tileY, _state.Camera);
        }

        if (_state.HasSelectedWall)
        {
            _mapRenderer.DrawSelectedWallHighlight(_state.SelectedWallTileX, _state.SelectedWallTileY, _state.Camera);
            var secret = _state.GetSelectedSecretPlacement();
            if (secret != null)
                _mapRenderer.DrawSecretWallPreview(secret, _state.Camera);
        }

        // ImGui panels
        rlImGui.Begin();
        bool menuToggleSim = _gui.RenderMenuBar(_state.IsSimulating, _state, _state.EnemySystem, _state.DoorSystem, _state.ClearLevel, _state.RefreshLayerReferences);
        if (menuToggleSim) _state.ToggleSimulation();
        _gui.RenderFileDialogs(_state.RefreshLayerReferences);
        _gui.RenderLayerPanel(_state.Layers, ref _state.ActiveLayerIndex);
        _gui.RenderTilePalette(_state.Layers, _state.ActiveLayerIndex, _state, ref _state.SelectedTileId, ref _state.SelectedPickupType);
        _gui.RenderPickupPalette(_state);
        _gui.RenderInfoPanel(tileX, tileY, worldPos, tileInBounds, _state.CursorInfoFollowsMouse, _state.Layers);
        _gui.RenderEntityPropertiesPanel(_state, ref _state.SelectedEnemyIndex, ref _state.IsEditingPatrolPath, ref _state.PatrolEditEnemyIndex, _state.PatrolPathInProgress);
        _gui.RenderPickupPropertiesPanel(ref _state.SelectedPickupIndex);
        _gui.RenderWallPropertiesPanel(_state);
        _gui.RenderDebugLogPanel();
        _gui.RenderPathfindingPanel(_state);
        _gui.RenderSoundPropagationPanel(_state);
        rlImGui.End();

        DrawText("Level Editor - F1 to return to game", 10, GetScreenHeight() - 70, 20, Color.White);
        DrawText($"Zoom: {_state.Camera.Zoom:F2}x", 10, GetScreenHeight() - 45, 20, Color.LightGray);

        if (_state.IsEditingPatrolPath)
        {
            const string msg = "EDITING PATROL PATH - LMB: Add waypoint | Enter: Confirm | Esc: Cancel";
            int msgW = MeasureText(msg, 24);
            DrawText(msg, (GetScreenWidth() - msgW) / 2, GetScreenHeight() - 100, 24, Color.Yellow);
        }
        else if (_state.PathPickingMode != EditorState.PathPickMode.None)
        {
            string msg = _state.PathPickingMode == EditorState.PathPickMode.Start
                ? "PICKING PATHFINDING START - LMB: Set tile | Esc: Cancel"
                : "PICKING PATHFINDING END - LMB: Set tile | Esc: Cancel";
            int msgW = MeasureText(msg, 24);
            DrawText(msg, (GetScreenWidth() - msgW) / 2, GetScreenHeight() - 100, 24, Color.Yellow);
        }
        else if (_state.SoundPropagationPicking)
        {
            const string msg = "TEST SOUND PROPAGATION - LMB: Set origin | Esc: Cancel";
            int msgW = MeasureText(msg, 24);
            DrawText(msg, (GetScreenWidth() - msgW) / 2, GetScreenHeight() - 100, 24, Color.Yellow);
        }
        else if (_state.IsWallSelectMode)
        {
            const string msg = "SELECT MODE - LMB: Select wall | B: Paint mode";
            int msgW = MeasureText(msg, 24);
            DrawText(msg, (GetScreenWidth() - msgW) / 2, GetScreenHeight() - 100, 24, Color.SkyBlue);
        }

        _gui.DrawStatusMessage();

        EndDrawing();
    }

    // ─── Input Helpers ───────────────────────────────────────────────────────────

    private void HandlePatrolPathInput(bool mouseOverUI)
    {
        if (!mouseOverUI && IsMouseButtonPressed(MouseButton.Left))
        {
            var paintPos = _state.Camera.ScreenToWorld(GetMousePosition());
            int px = (int)MathF.Floor(paintPos.X);
            int py = (int)MathF.Floor(paintPos.Y);
            _state.AddPatrolWaypoint(px, py);
        }

        if (IsKeyPressed(KeyboardKey.Enter) || IsKeyPressed(KeyboardKey.KpEnter))
        {
            _state.ConfirmPatrolPath();
        }

        if (IsKeyPressed(KeyboardKey.Escape))
        {
            _state.CancelPatrolPath();
        }
    }

    private void HandlePlayerInput(bool mouseOverUI)
    {
        if (_state.IsSimulating) return;

        var mouseScreen = GetMousePosition();
        _state.UpdatePlayerHover(_state.Camera, mouseScreen, mouseOverUI);

        if (!mouseOverUI && IsMouseButtonPressed(MouseButton.Left) && _state.HoveredPlayer)
            _state.SelectPlayer();

        if (_state.IsDraggingPlayer && IsMouseButtonDown(MouseButton.Left))
        {
            var dragPos = _state.Camera.ScreenToWorld(GetMousePosition());
            int tx = (int)MathF.Floor(dragPos.X);
            int ty = (int)MathF.Floor(dragPos.Y);
            if (tx >= 0 && tx < _state.MapData.Width && ty >= 0 && ty < _state.MapData.Height
                && _state.CanPlacePickupAt(tx, ty))
            {
                _state.SyncPlayerToSpawnTile(tx, ty);
            }
        }

        if (IsMouseButtonReleased(MouseButton.Left))
            _state.IsDraggingPlayer = false;
    }

    private void HandleTileAndEnemyInput(bool mouseOverUI)
    {
        if (!_state.IsSimulating)
            HandlePlayerInput(mouseOverUI);

        bool isEnemyLayer = _state.IsOnEnemyLayer;
        bool isPickupLayer = _state.IsOnPickupLayer;

        if (!mouseOverUI && !isEnemyLayer && _state.HoveredEnemyIndex >= 0 && IsMouseButtonPressed(MouseButton.Left))
        {
            _state.SwitchToEnemyLayer();
            isEnemyLayer = true;
            _state.SelectEnemy(_state.HoveredEnemyIndex);
        }

        if (!mouseOverUI && !isPickupLayer && _state.HoveredPickupIndex >= 0 && IsMouseButtonPressed(MouseButton.Left))
        {
            _state.SwitchToPickupLayer();
            isPickupLayer = true;
            _state.SelectPickup(_state.HoveredPickupIndex);
        }

        if (!mouseOverUI && isEnemyLayer && IsMouseButtonPressed(MouseButton.Left))
        {
            if (_state.HoveredPickupIndex >= 0)
            {
                _state.SwitchToPickupLayer();
                isEnemyLayer = false;
                isPickupLayer = true;
                _state.SelectPickup(_state.HoveredPickupIndex);
            }
            else
            {
                var clickPos = _state.Camera.ScreenToWorld(GetMousePosition());
                int cx = (int)MathF.Floor(clickPos.X);
                int cy = (int)MathF.Floor(clickPos.Y);
                uint doorTile = _state.GetDoorTileAt(cx, cy);
                if (doorTile != 0)
                {
                    _state.SwitchToDoorLayer();
                    _state.SelectedTileId = doorTile;
                    isEnemyLayer = false;
                }
            }
        }

        if (!mouseOverUI && isEnemyLayer && !_state.IsDraggingPlayer)
        {
            HandleEnemyInput();
        }
        else if (!mouseOverUI && isPickupLayer && !_state.IsDraggingPlayer)
        {
            HandlePickupInput();
        }
        else if (!mouseOverUI && !_state.IsDraggingPlayer && !isEnemyLayer && !isPickupLayer)
        {
            if (_state.IsWallSelectMode)
                HandleWallSelectInput(mouseOverUI);
            else if (IsMouseButtonDown(MouseButton.Left))
            {
                var paintPos = _state.Camera.ScreenToWorld(GetMousePosition());
                int px = (int)MathF.Floor(paintPos.X);
                int py = (int)MathF.Floor(paintPos.Y);
                _state.PaintTile(px, py);
            }
        }

        if (isEnemyLayer && _state.SelectedEnemyIndex >= 0 && _state.SelectedEnemyIndex < _state.MapData.Enemies.Count
            && IsKeyPressed(KeyboardKey.Delete))
        {
            _state.DeleteSelectedEnemy();
        }

        if (isPickupLayer && _state.SelectedPickupIndex >= 0 && _state.SelectedPickupIndex < _state.MapData.Pickups.Count
            && IsKeyPressed(KeyboardKey.Delete))
        {
            _state.DeleteSelectedPickup();
        }
    }

    private void HandleWallSelectInput(bool mouseOverUI)
    {
        if (mouseOverUI || !IsMouseButtonPressed(MouseButton.Left)) return;

        var clickPos = _state.Camera.ScreenToWorld(GetMousePosition());
        int px = (int)MathF.Floor(clickPos.X);
        int py = (int)MathF.Floor(clickPos.Y);

        if (_state.GetWallTileAt(px, py) > 0)
            _state.SelectWallTile(px, py);
        else
            _state.ClearWallSelection();
    }

    private void HandlePickupInput()
    {
        if (IsMouseButtonPressed(MouseButton.Left))
        {
            if (_state.HoveredPickupIndex >= 0)
                _state.SelectPickup(_state.HoveredPickupIndex);
            else
            {
                var paintPos = _state.Camera.ScreenToWorld(GetMousePosition());
                int px = (int)MathF.Floor(paintPos.X);
                int py = (int)MathF.Floor(paintPos.Y);
                if (!_state.TrySwitchLayerFromPickupClick(px, py))
                    _state.PlacePickup(px, py);
            }
        }

        if (_state.IsDraggingPickup && IsMouseButtonDown(MouseButton.Left)
            && _state.SelectedPickupIndex >= 0 && _state.SelectedPickupIndex < _state.MapData.Pickups.Count)
        {
            var dragPos = _state.Camera.ScreenToWorld(GetMousePosition());
            int dx = (int)MathF.Floor(dragPos.X);
            int dy = (int)MathF.Floor(dragPos.Y);
            _state.MovePickup(dx, dy);
        }

        if (IsMouseButtonReleased(MouseButton.Left))
            _state.IsDraggingPickup = false;
    }

    private void HandleEnemyInput()
    {
        if (IsMouseButtonPressed(MouseButton.Left))
        {
            if (_state.HoveredEnemyIndex >= 0)
            {
                _state.SelectEnemy(_state.HoveredEnemyIndex);
            }
            else
            {
                var paintPos = _state.Camera.ScreenToWorld(GetMousePosition());
                int px = (int)MathF.Floor(paintPos.X);
                int py = (int)MathF.Floor(paintPos.Y);
                _state.PlaceEnemy(px, py);
            }
        }

        if (_state.IsDraggingEnemy && IsMouseButtonDown(MouseButton.Left)
            && _state.SelectedEnemyIndex >= 0 && _state.SelectedEnemyIndex < _state.MapData.Enemies.Count)
        {
            var dragPos = _state.Camera.ScreenToWorld(GetMousePosition());
            int dx = (int)MathF.Floor(dragPos.X);
            int dy = (int)MathF.Floor(dragPos.Y);
            _state.MoveEnemy(dx, dy);
        }

        if (IsMouseButtonReleased(MouseButton.Left))
        {
            _state.IsDraggingEnemy = false;
        }
    }

    // ─── Simulation Helpers ─────────────────────────────────────────────────────

    private void UpdatePlayerMovement(float deltaTime)
    {
        const float rotationSpeed = 2.5f;

        var camera = _state.Player.Camera;
        Vector3 forward = Vector3.Normalize(camera.Target - camera.Position);

        float yawDelta = 0;
        if (IsKeyDown(KeyboardKey.Left))  yawDelta += rotationSpeed * deltaTime;
        if (IsKeyDown(KeyboardKey.Right)) yawDelta -= rotationSpeed * deltaTime;

        if (MathF.Abs(yawDelta) > 0.0001f)
        {
            var rotMatrix = Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, yawDelta);
            forward = Vector3.Transform(forward, rotMatrix);
        }

        Vector3 forwardXZ = new Vector3(forward.X, 0, forward.Z);
        float forwardLen = forwardXZ.Length();
        if (forwardLen > 0.001f)
            forwardXZ /= forwardLen;
        else
            forwardXZ = Vector3.UnitZ;

        Vector3 right = Vector3.Cross(forwardXZ, -Vector3.UnitY);
        float rightLen = right.Length();
        if (rightLen > 0.001f)
            right /= rightLen;

        Vector3 moveDir = Vector3.Zero;
        if (IsKeyDown(KeyboardKey.W)) moveDir += forwardXZ;
        if (IsKeyDown(KeyboardKey.S)) moveDir -= forwardXZ;
        if (IsKeyDown(KeyboardKey.A)) moveDir += right;
        if (IsKeyDown(KeyboardKey.D)) moveDir -= right;

        float moveDirLen = moveDir.Length();
        if (moveDirLen > 0.001f)
        {
            moveDir /= moveDirLen;
            var oldPosition = _state.Player.Position;
            var desired = oldPosition + moveDir * _state.Player.MoveSpeed * deltaTime;
            _state.Player.OldPosition = oldPosition;
            _state.Player.Position = _collisionSystem.ResolveMovement(
                oldPosition, desired, _state.Player.CollisionRadius);
        }

        camera.Position = _state.Player.Position;
        camera.Target = camera.Position + forward;
        _state.Player.Camera = camera;
    }
}
