using System.Numerics;
using Game.Editor.Undo;
using Game.Features.Players;

namespace Game.Editor;

/// <summary>Player spawn selection, drag, and property edits in the level editor.</summary>
public sealed class PlayerSpawnEditorTool
{
    private readonly MapData _mapData;
    private readonly Player _player;
    private readonly EditorUndoStack _undoStack;
    private readonly Action _notifyChanged;
    private readonly Action _deselectOthers;

    private int _dragStartX;
    private int _dragStartY;
    private float _dragStartRotation;

    public bool IsHovered { get; set; }
    public bool IsSelected { get; set; }
    public bool IsDragging { get; set; }

    public PlayerSpawnEditorTool(
        MapData mapData,
        Player player,
        EditorUndoStack undoStack,
        Action notifyChanged,
        Action deselectOthers)
    {
        _mapData = mapData;
        _player = player;
        _undoStack = undoStack;
        _notifyChanged = notifyChanged;
        _deselectOthers = deselectOthers;
    }

    public void Deselect()
    {
        IsSelected = false;
        IsDragging = false;
    }

    public void Select()
    {
        _deselectOthers();
        IsSelected = true;
        IsDragging = true;
        _dragStartX = _mapData.Spawn.TileX;
        _dragStartY = _mapData.Spawn.TileY;
        _dragStartRotation = _mapData.Spawn.Rotation;
        _notifyChanged();
    }

    public void EndDrag()
    {
        if (!IsSelected) return;

        if (_mapData.Spawn.TileX != _dragStartX
            || _mapData.Spawn.TileY != _dragStartY
            || _mapData.Spawn.Rotation != _dragStartRotation)
        {
            _undoStack.Push(new SetPlayerSpawnCommand(
                _dragStartX, _dragStartY, _dragStartRotation,
                _mapData.Spawn.TileX, _mapData.Spawn.TileY, _mapData.Spawn.Rotation));
            _notifyChanged();
        }
    }

    public void ApplyFromMap()
    {
        PlayerSpawn.ApplyFromMap(_player, _mapData, PlayerSpawnApplyMode.PositionAndCameraOnly);
        _notifyChanged();
    }

    public void SyncToSpawnTile(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= _mapData.Width || tileY < 0 || tileY >= _mapData.Height)
            return;

        int oldX = _mapData.Spawn.TileX;
        int oldY = _mapData.Spawn.TileY;
        float oldRotation = _mapData.Spawn.Rotation;
        if (oldX == tileX && oldY == tileY)
            return;

        _mapData.Spawn.TileX = tileX;
        _mapData.Spawn.TileY = tileY;
        PlayerSpawn.ApplyFromMap(_player, _mapData, PlayerSpawnApplyMode.PositionAndCameraOnly);

        if (!IsDragging)
        {
            _undoStack.Push(new SetPlayerSpawnCommand(
                oldX, oldY, oldRotation, tileX, tileY, _mapData.Spawn.Rotation));
        }

        _notifyChanged();
    }

    public void SetRotationIndex(int rotIndex)
    {
        const float step = MathF.PI / 4f;
        int oldX = _mapData.Spawn.TileX;
        int oldY = _mapData.Spawn.TileY;
        float oldRotation = _mapData.Spawn.Rotation;
        float newRotation = Math.Clamp(rotIndex, 0, 7) * step;
        if (MathF.Abs(oldRotation - newRotation) < 0.0001f)
            return;

        _mapData.Spawn.Rotation = newRotation;
        PlayerSpawn.ApplyCameraFromMap(_player, _mapData);
        _undoStack.Push(new SetPlayerSpawnCommand(
            oldX, oldY, oldRotation, oldX, oldY, newRotation));
        _notifyChanged();
    }

    public void ApplyRotation() =>
        PlayerSpawn.ApplyCameraFromMap(_player, _mapData);

    public static int GetRotationIndex(float rotationRadians)
    {
        const float step = MathF.PI / 4f;
        return Math.Clamp((int)MathF.Round(rotationRadians / step), 0, 7);
    }

    public void UpdateHover(EditorCamera camera, Vector2 mouseScreen, bool isMouseOverUI)
    {
        IsHovered = false;
        if (isMouseOverUI) return;

        float tileSize = camera.TileSize;
        float quadSize = LevelData.QuadSize;
        float tileX = _player.Position.X / quadSize;
        float tileY = _player.Position.Z / quadSize;
        float centerX = (tileX + 0.5f) * tileSize + camera.Offset.X;
        float centerY = (tileY + 0.5f) * tileSize + camera.Offset.Y;
        float radius = tileSize * 0.35f;
        float dx = mouseScreen.X - centerX;
        float dy = mouseScreen.Y - centerY;
        IsHovered = dx * dx + dy * dy <= radius * radius;
    }
}
