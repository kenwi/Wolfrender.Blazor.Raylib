using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;
using Color = Raylib_cs.Color;

namespace Game.Editor;

/// <summary>
/// Level editor scene for the web/Blazor build. Uses EditorState, EditorCamera,
/// and EditorMapRenderer for all logic and rendering. Has zero ImGui/rlImGui dependencies.
/// The HTML UI is provided by Blazor components that bind to EditorState.
/// </summary>
public class WebEditorScene : IScene
{
    public readonly EditorState State;
    private readonly EditorMapRenderer _mapRenderer;
    private readonly CollisionSystem _collisionSystem;

    public WebEditorScene(MapData mapData, EnemySystem enemySystem,
        DoorSystem doorSystem, Player player)
    {
        State = new EditorState(mapData, enemySystem, doorSystem, player);
        _mapRenderer = new EditorMapRenderer(mapData);
        _collisionSystem = new CollisionSystem(new LevelData(mapData), doorSystem);
    }

    public void OnEnter()
    {
        ShowCursor();
    }

    public void OnExit()
    {
        State.IsSimulating = false;
    }

    public void Update(float deltaTime)
    {
        if (IsKeyPressed(KeyboardKey.C))
        {
            State.CursorInfoFollowsMouse = !State.CursorInfoFollowsMouse;
        }

        bool ctrlHeld = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);
        KeyboardKey[] numKeys = {
            KeyboardKey.One, KeyboardKey.Two, KeyboardKey.Three,
            KeyboardKey.Four, KeyboardKey.Five, KeyboardKey.Six,
            KeyboardKey.Seven, KeyboardKey.Eight, KeyboardKey.Nine
        };
        for (int i = 0; i < numKeys.Length && i < State.Layers.Count; i++)
        {
            if (IsKeyPressed(numKeys[i]))
            {
                if (ctrlHeld)
                    State.Layers[i].IsVisible = !State.Layers[i].IsVisible;
                else
                    State.ActiveLayerIndex = i;
            }
        }

        State.UpdateStatusTimer(deltaTime);

        if (IsKeyPressed(KeyboardKey.P))
        {
            State.ToggleSimulation();
        }

        if (State.IsSimulating)
        {
            UpdatePlayerMovement(deltaTime);
            State.EnemySystem.Update(deltaTime);
            State.UpdateDoorsDuringSimulation(deltaTime, IsKeyPressed(KeyboardKey.E));
        }

        State.Camera.HandleInput(deltaTime, ctrlHeld, State.IsMouseOverUI, disableKeyboardPan: State.IsSimulating);

        if (State.IsEditingPatrolPath)
        {
            HandlePatrolPathInput();
        }
        else
        {
            HandleTileAndEnemyInput();
        }
    }

    public void Render()
    {
        BeginDrawing();
        ClearBackground(new Color(40, 40, 40, 255));

        _mapRenderer.DrawMapGrid(State.Camera);

        for (int i = 0; i < State.Layers.Count; i++)
        {
            var layer = State.Layers[i];
            if (!layer.IsVisible) continue;

            if (layer.Name == EditorState.EnemiesLayerName)
            {
                _mapRenderer.RenderEnemyLayer(
                    State.Camera, State.EnemySystem, State.IsMouseOverUI,
                    State.IsSimulating, State.DrawEnemyLineOfSight, State.ShowPatrolPaths,
                    ref State.HoveredEnemyIndex, State.SelectedEnemyIndex,
                    State.IsEditingPatrolPath, State.PatrolEditEnemyIndex, State.PatrolPathInProgress);
            }
            else if (layer.Name == EditorState.PickupsLayerName)
            {
                _mapRenderer.RenderPickupLayer(
                    State.Camera, State.IsMouseOverUI,
                    ref State.HoveredPickupIndex, State.SelectedPickupIndex);
            }
            else if (layer.Name == EditorState.ObjectsLayerName)
            {
                _mapRenderer.RenderObjectLayer(State.Camera);
            }
            else if (State.IsSimulating && layer.Name == EditorState.DoorsLayerName)
            {
                _mapRenderer.RenderLiveDoors(State.DoorSystem, State.Camera);
            }
            else
            {
                _mapRenderer.RenderLayer(layer, State.Camera);
            }
        }

        _mapRenderer.RenderPlayerIndicator(
            State.Player, State.Camera, State.MapData.PlayerSpawnRotation,
            State.HoveredPlayer, State.IsPlayerSelected, State.IsDraggingPlayer);

        var mouseScreen = GetMousePosition();
        var worldPos = State.Camera.ScreenToWorld(mouseScreen);
        int tileX = (int)MathF.Floor(worldPos.X);
        int tileY = (int)MathF.Floor(worldPos.Y);
        bool tileInBounds = tileX >= 0 && tileX < State.MapData.Width && tileY >= 0 && tileY < State.MapData.Height;

        if (tileInBounds && State.ShouldShowTileHighlight())
        {
            _mapRenderer.DrawTileHighlight(tileX, tileY, State.Camera);
        }

        DrawText("Level Editor - Tab to return to game", 10, GetScreenHeight() - 70, 20, Color.White);
        DrawText($"Zoom: {State.Camera.Zoom:F2}x", 10, GetScreenHeight() - 45, 20, Color.LightGray);

        if (State.IsEditingPatrolPath)
        {
            const string msg = "EDITING PATROL PATH - LMB: Add waypoint | Enter: Confirm | Esc: Cancel";
            int msgW = MeasureText(msg, 24);
            DrawText(msg, (GetScreenWidth() - msgW) / 2, GetScreenHeight() - 100, 24, Color.Yellow);
        }

        // Status message
        if (State.StatusTimer > 0 && !string.IsNullOrEmpty(State.StatusMessage))
        {
            const int fontSize = 30;
            int textWidth = MeasureText(State.StatusMessage, fontSize);
            int x = (GetScreenWidth() - textWidth) / 2;
            var statusColor = State.StatusMessage.StartsWith("Error") ? Color.Red : Color.Green;
            DrawText(State.StatusMessage, x, 55, fontSize, statusColor);
        }

        EndDrawing();
    }

    private void HandlePatrolPathInput()
    {
        if (!State.IsMouseOverUI && IsMouseButtonPressed(MouseButton.Left))
        {
            var paintPos = State.Camera.ScreenToWorld(GetMousePosition());
            int px = (int)MathF.Floor(paintPos.X);
            int py = (int)MathF.Floor(paintPos.Y);
            State.AddPatrolWaypoint(px, py);
        }

        if (IsKeyPressed(KeyboardKey.Enter) || IsKeyPressed(KeyboardKey.KpEnter))
            State.ConfirmPatrolPath();

        if (IsKeyPressed(KeyboardKey.Escape))
            State.CancelPatrolPath();
    }

    private void HandlePlayerInput()
    {
        if (State.IsSimulating) return;

        var mouseScreen = GetMousePosition();
        State.UpdatePlayerHover(State.Camera, mouseScreen, State.IsMouseOverUI);

        if (!State.IsMouseOverUI && IsMouseButtonPressed(MouseButton.Left) && State.HoveredPlayer)
            State.SelectPlayer();

        if (State.IsDraggingPlayer && IsMouseButtonDown(MouseButton.Left))
        {
            var dragPos = State.Camera.ScreenToWorld(GetMousePosition());
            int tx = (int)MathF.Floor(dragPos.X);
            int ty = (int)MathF.Floor(dragPos.Y);
            if (tx >= 0 && tx < State.MapData.Width && ty >= 0 && ty < State.MapData.Height
                && State.CanPlacePickupAt(tx, ty))
            {
                State.SyncPlayerToSpawnTile(tx, ty);
            }
        }

        if (IsMouseButtonReleased(MouseButton.Left))
            State.IsDraggingPlayer = false;
    }

    private void HandleTileAndEnemyInput()
    {
        if (!State.IsSimulating)
            HandlePlayerInput();

        bool isEnemyLayer = State.IsOnEnemyLayer;
        bool isPickupLayer = State.IsOnPickupLayer;

        if (!State.IsMouseOverUI && !isEnemyLayer && State.HoveredEnemyIndex >= 0 && IsMouseButtonPressed(MouseButton.Left))
        {
            State.SwitchToEnemyLayer();
            isEnemyLayer = true;
            State.SelectEnemy(State.HoveredEnemyIndex);
        }

        if (!State.IsMouseOverUI && !isPickupLayer && State.HoveredPickupIndex >= 0 && IsMouseButtonPressed(MouseButton.Left))
        {
            State.SwitchToPickupLayer();
            isPickupLayer = true;
            State.SelectPickup(State.HoveredPickupIndex);
        }

        if (!State.IsMouseOverUI && isEnemyLayer && IsMouseButtonPressed(MouseButton.Left))
        {
            if (State.HoveredPickupIndex >= 0)
            {
                State.SwitchToPickupLayer();
                isEnemyLayer = false;
                isPickupLayer = true;
                State.SelectPickup(State.HoveredPickupIndex);
            }
            else
            {
                var clickPos = State.Camera.ScreenToWorld(GetMousePosition());
                int cx = (int)MathF.Floor(clickPos.X);
                int cy = (int)MathF.Floor(clickPos.Y);
                uint doorTile = State.GetDoorTileAt(cx, cy);
                if (doorTile != 0)
                {
                    State.SwitchToDoorLayer();
                    State.SelectedTileId = doorTile;
                    isEnemyLayer = false;
                }
            }
        }

        if (!State.IsMouseOverUI && isEnemyLayer && !State.IsDraggingPlayer)
        {
            HandleEnemyInput();
        }
        else if (!State.IsMouseOverUI && isPickupLayer && !State.IsDraggingPlayer)
        {
            HandlePickupInput();
        }
        else if (!State.IsMouseOverUI && IsMouseButtonDown(MouseButton.Left) && !isEnemyLayer && !isPickupLayer
                 && !State.IsDraggingPlayer)
        {
            var paintPos = State.Camera.ScreenToWorld(GetMousePosition());
            int px = (int)MathF.Floor(paintPos.X);
            int py = (int)MathF.Floor(paintPos.Y);
            State.PaintTile(px, py);
        }

        if (isEnemyLayer && State.SelectedEnemyIndex >= 0 && State.SelectedEnemyIndex < State.MapData.Enemies.Count
            && IsKeyPressed(KeyboardKey.Delete))
        {
            State.DeleteSelectedEnemy();
        }

        if (isPickupLayer && State.SelectedPickupIndex >= 0 && State.SelectedPickupIndex < State.MapData.Pickups.Count
            && IsKeyPressed(KeyboardKey.Delete))
        {
            State.DeleteSelectedPickup();
        }
    }

    private void HandlePickupInput()
    {
        if (IsMouseButtonPressed(MouseButton.Left))
        {
            if (State.HoveredPickupIndex >= 0)
                State.SelectPickup(State.HoveredPickupIndex);
            else
            {
                var paintPos = State.Camera.ScreenToWorld(GetMousePosition());
                int px = (int)MathF.Floor(paintPos.X);
                int py = (int)MathF.Floor(paintPos.Y);
                if (!State.TrySwitchLayerFromPickupClick(px, py))
                    State.PlacePickup(px, py);
            }
        }

        if (State.IsDraggingPickup && IsMouseButtonDown(MouseButton.Left)
            && State.SelectedPickupIndex >= 0 && State.SelectedPickupIndex < State.MapData.Pickups.Count)
        {
            var dragPos = State.Camera.ScreenToWorld(GetMousePosition());
            int dx = (int)MathF.Floor(dragPos.X);
            int dy = (int)MathF.Floor(dragPos.Y);
            State.MovePickup(dx, dy);
        }

        if (IsMouseButtonReleased(MouseButton.Left))
            State.IsDraggingPickup = false;
    }

    private void HandleEnemyInput()
    {
        if (IsMouseButtonPressed(MouseButton.Left))
        {
            if (State.HoveredEnemyIndex >= 0)
                State.SelectEnemy(State.HoveredEnemyIndex);
            else
            {
                var paintPos = State.Camera.ScreenToWorld(GetMousePosition());
                int px = (int)MathF.Floor(paintPos.X);
                int py = (int)MathF.Floor(paintPos.Y);
                State.PlaceEnemy(px, py);
            }
        }

        if (State.IsDraggingEnemy && IsMouseButtonDown(MouseButton.Left)
            && State.SelectedEnemyIndex >= 0 && State.SelectedEnemyIndex < State.MapData.Enemies.Count)
        {
            var dragPos = State.Camera.ScreenToWorld(GetMousePosition());
            int dx = (int)MathF.Floor(dragPos.X);
            int dy = (int)MathF.Floor(dragPos.Y);
            State.MoveEnemy(dx, dy);
        }

        if (IsMouseButtonReleased(MouseButton.Left))
            State.IsDraggingEnemy = false;
    }

    private void UpdatePlayerMovement(float deltaTime)
    {
        const float rotationSpeed = 2.5f;

        var camera = State.Player.Camera;
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
            var oldPosition = State.Player.Position;
            var desired = oldPosition + moveDir * State.Player.MoveSpeed * deltaTime;
            State.Player.OldPosition = oldPosition;
            State.Player.Position = _collisionSystem.ResolveMovement(
                oldPosition, desired, State.Player.CollisionRadius);
        }

        camera.Position = State.Player.Position;
        camera.Target = camera.Position + forward;
        State.Player.Camera = camera;
    }
}
