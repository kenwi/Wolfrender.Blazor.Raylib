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
    private readonly EditorState _state;
    private readonly EditorMapRenderer _mapRenderer;
    private readonly EditorGui _gui;

    public LevelEditorScene(MapData mapData, EnemySystem enemySystem, DoorSystem doorSystem, Entities.Player player)
    {
        _state = new EditorState(mapData, enemySystem, doorSystem, player);
        _mapRenderer = new EditorMapRenderer(mapData);
        _gui = new EditorGui(mapData);
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
        _gui.HandleScalingInput();

        // Toggle simulation with P key
        if (IsKeyPressed(KeyboardKey.P))
        {
            _state.ToggleSimulation();
        }

        // Tick game systems when simulating
        if (_state.IsSimulating)
        {
            UpdatePlayerMovement(deltaTime);
            _state.EnemySystem.Update(deltaTime);
            _state.DoorSystem.Animate(deltaTime);
        }

        // Read ImGui IO state and propagate to EditorState
        bool imGuiWantsMouse = ImGui.GetIO().WantCaptureMouse;
        bool imGuiWantsKeyboard = ImGui.GetIO().WantCaptureKeyboard;
        _state.IsMouseOverUI = imGuiWantsMouse;

        // Camera input (pan + zoom) — disable WASD panning during simulation (player uses those keys)
        _state.Camera.HandleInput(deltaTime, ctrlHeld, imGuiWantsMouse, imGuiWantsKeyboard, disableKeyboardPan: _state.IsSimulating);

        // Patrol path editing mode
        if (_state.IsEditingPatrolPath)
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
                    _state.IsSimulating,
                    ref _state.HoveredEnemyIndex, _state.SelectedEnemyIndex,
                    _state.IsEditingPatrolPath, _state.PatrolEditEnemyIndex, _state.PatrolPathInProgress);
            }
            else if (_state.IsSimulating && layer.Name == "Doors")
            {
                _mapRenderer.RenderLiveDoors(_state.DoorSystem, _state.Camera);
            }
            else
            {
                _mapRenderer.RenderLayer(layer, _state.Camera);
            }
        }

        // Draw player position indicator
        _mapRenderer.RenderPlayerIndicator(_state.Player, _state.Camera);

        // Highlight hovered tile
        var mouseScreen = GetMousePosition();
        var worldPos = _state.Camera.ScreenToWorld(mouseScreen);
        int tileX = (int)MathF.Floor(worldPos.X);
        int tileY = (int)MathF.Floor(worldPos.Y);
        bool tileInBounds = tileX >= 0 && tileX < _state.MapData.Width && tileY >= 0 && tileY < _state.MapData.Height;

        if (tileInBounds)
        {
            _mapRenderer.DrawTileHighlight(tileX, tileY, _state.Camera);
        }

        // ImGui panels
        rlImGui.Begin();
        bool menuToggleSim = _gui.RenderMenuBar(_state.IsSimulating, _state.EnemySystem, _state.DoorSystem, _state.ClearLevel, _state.RefreshLayerReferences);
        if (menuToggleSim) _state.ToggleSimulation();
        _gui.RenderFileDialogs(_state.RefreshLayerReferences);
        _gui.RenderLayerPanel(_state.Layers, ref _state.ActiveLayerIndex);
        _gui.RenderTilePalette(_state.Layers, _state.ActiveLayerIndex, ref _state.SelectedTileId);
        _gui.RenderInfoPanel(tileX, tileY, worldPos, tileInBounds, _state.CursorInfoFollowsMouse, _state.Layers, EditorState.EnemiesLayerName);
        _gui.RenderEnemyPropertiesPanel(ref _state.SelectedEnemyIndex, ref _state.IsEditingPatrolPath, ref _state.PatrolEditEnemyIndex, _state.PatrolPathInProgress);
        _gui.RenderDebugLogPanel();
        rlImGui.End();

        DrawText("Level Editor - F1 to return to game", 10, GetScreenHeight() - 70, 20, Color.White);
        DrawText($"Zoom: {_state.Camera.Zoom:F2}x", 10, GetScreenHeight() - 45, 20, Color.LightGray);

        if (_state.IsEditingPatrolPath)
        {
            const string msg = "EDITING PATROL PATH - LMB: Add waypoint | Enter: Confirm | Esc: Cancel";
            int msgW = MeasureText(msg, 24);
            DrawText(msg, (GetScreenWidth() - msgW) / 2, GetScreenHeight() - 100, 24, Color.Yellow);
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

    private void HandleTileAndEnemyInput(bool mouseOverUI)
    {
        bool isEnemyLayer = _state.IsOnEnemyLayer;

        // If clicking on a hovered enemy while another layer is active, auto-switch to enemy layer
        if (!mouseOverUI && !isEnemyLayer && _state.HoveredEnemyIndex >= 0 && IsMouseButtonPressed(MouseButton.Left))
        {
            _state.SwitchToEnemyLayer();
            isEnemyLayer = true;
            _state.SelectedEnemyIndex = _state.HoveredEnemyIndex;
            _state.IsDraggingEnemy = true;
        }

        if (!mouseOverUI && isEnemyLayer)
        {
            HandleEnemyInput();
        }
        else if (!mouseOverUI && IsMouseButtonDown(MouseButton.Left) && !isEnemyLayer)
        {
            var paintPos = _state.Camera.ScreenToWorld(GetMousePosition());
            int px = (int)MathF.Floor(paintPos.X);
            int py = (int)MathF.Floor(paintPos.Y);
            _state.PaintTile(px, py);
        }

        // Delete selected enemy with Delete key
        if (isEnemyLayer && _state.SelectedEnemyIndex >= 0 && _state.SelectedEnemyIndex < _state.MapData.Enemies.Count
            && IsKeyPressed(KeyboardKey.Delete))
        {
            _state.DeleteSelectedEnemy();
        }
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
            _state.Player.Position += moveDir * _state.Player.MoveSpeed * deltaTime;
        }

        camera.Position = _state.Player.Position;
        camera.Target = camera.Position + forward;
        _state.Player.Camera = camera;
    }
}
