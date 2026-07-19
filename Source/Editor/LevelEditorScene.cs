using Game.Engine.Movement;
using Game.Features.Doors;
using Game.Features.Enemies;
using Game.Features.LevelProgress;
using Game.Features.Players;
using Game.Features.WorldObjects;
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
    private readonly EditorMapInteractionController _mapInteraction;

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
        var collisionSystem = new CollisionSystem(
            new LevelData(mapData),
            new CompositeMovementBlocker(doorSystem, secretSystem),
            ObjectCollisionRules.Instance);
        _mapInteraction = new EditorMapInteractionController(_state, collisionSystem);
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
                    _state.SetActiveLayerIndex(i);
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
            if (IsKeyPressed(KeyboardKey.Z) && ctrlHeld)
                _state.Undo();
            else if (IsKeyPressed(KeyboardKey.Y) && ctrlHeld)
                _state.Redo();
            else if (IsKeyPressed(KeyboardKey.S) && ctrlHeld)
                _gui.QuickSave(_state);
            else if (IsKeyPressed(KeyboardKey.Q) && ctrlHeld)
                CloseWindow();

            if (IsKeyPressed(KeyboardKey.B))
                _state.SetToolMode(EditorState.EditorToolMode.Paint);
            if (IsKeyPressed(KeyboardKey.V))
                _state.SetToolMode(EditorState.EditorToolMode.Select);
        }

        // Tick game systems when simulating
        if (_state.IsSimulating)
        {
            _mapInteraction.UpdatePlayerMovement(deltaTime);
            _state.EnemySystem.Update(deltaTime);
            bool interactPressed = !imGuiWantsKeyboard && IsKeyPressed(KeyboardKey.E);
            _state.UpdateInteractablesDuringSimulation(deltaTime, interactPressed);
        }
        _state.IsMouseOverUI = imGuiWantsMouse;

        // Camera input (pan + zoom) - disable WASD panning during simulation (player uses those keys)
        _state.Camera.HandleInput(deltaTime, ctrlHeld, imGuiWantsMouse, imGuiWantsKeyboard, disableKeyboardPan: _state.IsSimulating);

        // Drop the click-suppression latch as soon as the user lets go.
        if (IsMouseButtonReleased(MouseButton.Left))
        {
            _mapInteraction.EndLeftMouseGesture();
            _suppressMapClickUntilRelease = false;
        }

        // Patrol path editing mode
        if (_state.IsEditingPatrolPath)
        {
            _mapInteraction.HandlePatrolPathInput(imGuiWantsMouse);
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
            _mapInteraction.HandleMapInput(imGuiWantsMouse, supportsWallSelect: true);
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

        if (_state.ShowRoomOverlay)
            _mapRenderer.DrawRoomOverlay(_state.RoomMap, _state.Camera);

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
        bool menuToggleSim = _gui.RenderMenuBar(_state.IsSimulating, _state, _state.EnemySystem, _state.DoorSystem, _state.ClearLevel, _state.RefreshLayerReferences, CloseWindow);
        if (menuToggleSim) _state.ToggleSimulation();
        _gui.RenderFileDialogs(_state);
        _gui.RenderLayerPanel(_state.Layers, _state);
        _gui.RenderTilePalette(_state.Layers, _state.ActiveLayerIndex, _state, ref _state.SelectedTileId, ref _state.SelectedPickupType);
        _gui.RenderPickupPalette(_state);
        _gui.RenderInfoPanel(tileX, tileY, worldPos, tileInBounds, _state.CursorInfoFollowsMouse, _state.Layers);
        _gui.RenderEntityPropertiesPanel(_state, ref _state.SelectedEnemyIndex, ref _state.IsEditingPatrolPath, ref _state.PatrolEditEnemyIndex, _state.PatrolPathInProgress);
        _gui.RenderPickupPropertiesPanel(_state, ref _state.SelectedPickupIndex);
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
}
