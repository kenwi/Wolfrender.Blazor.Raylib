using System.Numerics;
using Game.Engine.Movement;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Editor;

/// <summary>
/// Shared Raylib map interaction for web and desktop editor scenes:
/// patrol editing, entity/pickup/player drag, tile paint, and simulation movement.
/// Platform shells keep UI capture, hotkeys, and desktop-only pick modes.
/// </summary>
public sealed class EditorMapInteractionController
{
    private readonly EditorState _state;
    private readonly CollisionSystem _collisionSystem;

    public EditorMapInteractionController(EditorState state, CollisionSystem collisionSystem)
    {
        _state = state;
        _collisionSystem = collisionSystem;
    }

    public void EndLeftMouseGesture()
    {
        _state.EndPaintStroke();
        _state.EndEnemyDrag();
        _state.EndPickupDrag();
        _state.EndPlayerDrag();
        _state.IsDraggingPlayer = false;
    }

    public void HandlePatrolPathInput(bool mouseOverUi)
    {
        if (!mouseOverUi && IsMouseButtonPressed(MouseButton.Left))
        {
            var paintPos = _state.Camera.ScreenToWorld(GetMousePosition());
            int px = (int)MathF.Floor(paintPos.X);
            int py = (int)MathF.Floor(paintPos.Y);
            _state.AddPatrolWaypoint(px, py);
        }

        if (IsKeyPressed(KeyboardKey.Enter) || IsKeyPressed(KeyboardKey.KpEnter))
            _state.ConfirmPatrolPath();

        if (IsKeyPressed(KeyboardKey.Escape))
            _state.CancelPatrolPath();
    }

    /// <param name="supportsWallSelect">
    /// Desktop Select tool only. Web has no wall-select mode UI.
    /// </param>
    public void HandleMapInput(bool mouseOverUi, bool supportsWallSelect)
    {
        if (!_state.IsSimulating)
            HandlePlayerInput(mouseOverUi);

        bool isEnemyLayer = _state.IsOnEnemyLayer;
        bool isPickupLayer = _state.IsOnPickupLayer;

        if (!mouseOverUi && !isEnemyLayer && _state.HoveredEnemyIndex >= 0 && IsMouseButtonPressed(MouseButton.Left))
        {
            _state.SwitchToEnemyLayer();
            isEnemyLayer = true;
            _state.SelectEnemy(_state.HoveredEnemyIndex);
        }

        if (!mouseOverUi && !isPickupLayer && _state.HoveredPickupIndex >= 0 && IsMouseButtonPressed(MouseButton.Left))
        {
            _state.SwitchToPickupLayer();
            isPickupLayer = true;
            _state.SelectPickup(_state.HoveredPickupIndex);
        }

        if (!mouseOverUi && isEnemyLayer && IsMouseButtonPressed(MouseButton.Left))
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

        if (!mouseOverUi && isEnemyLayer && !_state.IsDraggingPlayer)
        {
            HandleEnemyInput();
        }
        else if (!mouseOverUi && isPickupLayer && !_state.IsDraggingPlayer)
        {
            HandlePickupInput();
        }
        else if (!mouseOverUi && !_state.IsDraggingPlayer && !isEnemyLayer && !isPickupLayer)
        {
            if (supportsWallSelect && _state.IsWallSelectMode)
                HandleWallSelectInput(mouseOverUi);
            else if (IsMouseButtonPressed(MouseButton.Left))
            {
                _state.BeginPaintStroke();
                var paintPos = _state.Camera.ScreenToWorld(GetMousePosition());
                int px = (int)MathF.Floor(paintPos.X);
                int py = (int)MathF.Floor(paintPos.Y);
                _state.PaintTile(px, py);
            }
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

    public void UpdatePlayerMovement(float deltaTime)
    {
        const float rotationSpeed = 2.5f;

        var camera = _state.Player.Camera;
        Vector3 forward = Vector3.Normalize(camera.Target - camera.Position);

        float yawDelta = 0;
        if (IsKeyDown(KeyboardKey.Left)) yawDelta += rotationSpeed * deltaTime;
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

    private void HandlePlayerInput(bool mouseOverUi)
    {
        if (_state.IsSimulating) return;

        var mouseScreen = GetMousePosition();
        _state.UpdatePlayerHover(_state.Camera, mouseScreen, mouseOverUi);

        if (!mouseOverUi && IsMouseButtonPressed(MouseButton.Left) && _state.HoveredPlayer)
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
                _state.SelectEnemy(_state.HoveredEnemyIndex);
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
            _state.IsDraggingEnemy = false;
    }

    private void HandleWallSelectInput(bool mouseOverUi)
    {
        if (mouseOverUi || !IsMouseButtonPressed(MouseButton.Left)) return;

        var clickPos = _state.Camera.ScreenToWorld(GetMousePosition());
        int px = (int)MathF.Floor(clickPos.X);
        int py = (int)MathF.Floor(clickPos.Y);

        if (_state.GetWallTileAt(px, py) > 0)
            _state.SelectWallTile(px, py);
        else
            _state.ClearWallSelection();
    }
}
