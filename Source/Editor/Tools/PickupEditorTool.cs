using Game.Editor.Undo;
using Game.Features.Pickups;

namespace Game.Editor;

/// <summary>Pickup placement selection, drag, and property edits.</summary>
public sealed class PickupEditorTool
{
    private readonly MapData _mapData;
    private readonly EditorUndoStack _undoStack;
    private readonly Action _notifyChanged;
    private readonly Action<string> _setStatus;
    private readonly Func<int, int, bool> _canPlaceAt;
    private readonly Action _deselectOthers;

    private int _dragIndex = -1;
    private int _dragStartX;
    private int _dragStartY;
    private int? _dragRemovedIndex;
    private PickupPlacementData? _dragRemovedPlacement;

    public PickupType SelectedType { get; set; } = PickupType.Health;
    public int HoveredIndex { get; set; } = -1;
    public int SelectedIndex { get; set; } = -1;
    public bool IsDragging { get; set; }

    public PickupEditorTool(
        MapData mapData,
        EditorUndoStack undoStack,
        Action notifyChanged,
        Action<string> setStatus,
        Func<int, int, bool> canPlaceAt,
        Action deselectOthers)
    {
        _mapData = mapData;
        _undoStack = undoStack;
        _notifyChanged = notifyChanged;
        _setStatus = setStatus;
        _canPlaceAt = canPlaceAt;
        _deselectOthers = deselectOthers;
    }

    public int FindIndexAt(int tileX, int tileY) =>
        _mapData.Pickups.FindIndex(p => p.TileX == tileX && p.TileY == tileY);

    public void Place(int x, int y)
    {
        if (x < 0 || x >= _mapData.Width || y < 0 || y >= _mapData.Height) return;
        if (!_canPlaceAt(x, y))
        {
            _setStatus("Cannot place pickup on walls or doors");
            return;
        }

        int? replacedIndex = null;
        PickupPlacementData? replacedPlacement = null;
        int existing = FindIndexAt(x, y);
        if (existing >= 0)
        {
            replacedIndex = existing;
            replacedPlacement = PickupPlacementData.FromPlacement(_mapData.Pickups[existing]);
            _mapData.Pickups.RemoveAt(existing);
        }

        var placement = new PickupPlacement
        {
            TileX = x,
            TileY = y,
            Type = SelectedType
        };
        _mapData.Pickups.Add(placement);
        int index = _mapData.Pickups.Count - 1;
        _undoStack.Push(new PlacePickupCommand(
            index,
            PickupPlacementData.FromPlacement(placement),
            replacedIndex,
            replacedPlacement));
        _deselectOthers();
        SelectedIndex = index;
        IsDragging = true;
        BeginDrag();
        _setStatus($"Placed {SelectedType} pickup");
        _notifyChanged();
    }

    public void Deselect()
    {
        SelectedIndex = -1;
        IsDragging = false;
    }

    public void Select(int index)
    {
        _deselectOthers();
        SelectedIndex = index;
        IsDragging = true;
        if (index >= 0 && index < _mapData.Pickups.Count)
            SelectedType = _mapData.Pickups[index].Type;
        BeginDrag();
        _notifyChanged();
    }

    public void BeginDrag()
    {
        if (SelectedIndex < 0 || SelectedIndex >= _mapData.Pickups.Count) return;
        var pickup = _mapData.Pickups[SelectedIndex];
        _dragIndex = SelectedIndex;
        _dragStartX = pickup.TileX;
        _dragStartY = pickup.TileY;
        _dragRemovedIndex = null;
        _dragRemovedPlacement = null;
    }

    public void EndDrag()
    {
        if (_dragIndex < 0 || _dragIndex >= _mapData.Pickups.Count)
        {
            _dragIndex = -1;
            _dragRemovedIndex = null;
            _dragRemovedPlacement = null;
            return;
        }

        var pickup = _mapData.Pickups[_dragIndex];
        if (pickup.TileX != _dragStartX || pickup.TileY != _dragStartY
            || _dragRemovedIndex.HasValue)
        {
            _undoStack.Push(new MovePickupCommand(
                _dragIndex,
                _dragStartX,
                _dragStartY,
                pickup.TileX,
                pickup.TileY,
                _dragRemovedIndex,
                _dragRemovedPlacement));
            _notifyChanged();
        }

        _dragIndex = -1;
        _dragRemovedIndex = null;
        _dragRemovedPlacement = null;
    }

    public void MoveSelected(int x, int y)
    {
        if (SelectedIndex < 0 || SelectedIndex >= _mapData.Pickups.Count) return;
        if (x < 0 || x >= _mapData.Width || y < 0 || y >= _mapData.Height) return;
        if (!_canPlaceAt(x, y)) return;

        var pickup = _mapData.Pickups[SelectedIndex];
        if (pickup.TileX == x && pickup.TileY == y)
            return;

        int occupant = FindIndexAt(x, y);
        if (occupant >= 0 && occupant != SelectedIndex)
        {
            if (!_dragRemovedIndex.HasValue)
            {
                _dragRemovedIndex = occupant;
                _dragRemovedPlacement = PickupPlacementData.FromPlacement(_mapData.Pickups[occupant]);
            }
            _mapData.Pickups.RemoveAt(occupant);
            if (SelectedIndex > occupant)
                SelectedIndex--;
            if (_dragIndex > occupant)
                _dragIndex--;
        }

        pickup.TileX = x;
        pickup.TileY = y;
        if (SelectedIndex >= _mapData.Pickups.Count)
            SelectedIndex = _mapData.Pickups.Count - 1;
    }

    public void DeleteSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= _mapData.Pickups.Count) return;
        DeleteAt(SelectedIndex);
    }

    public void DeleteAt(int index)
    {
        if (index < 0 || index >= _mapData.Pickups.Count) return;
        var data = PickupPlacementData.FromPlacement(_mapData.Pickups[index]);
        _mapData.Pickups.RemoveAt(index);
        _undoStack.Push(new RemovePickupCommand(index, data));
        if (SelectedIndex == index)
            SelectedIndex = -1;
        else if (SelectedIndex > index)
            SelectedIndex--;
        _notifyChanged();
    }

    public void SetTilePosition(int index, int tileX, int tileY)
    {
        if (index < 0 || index >= _mapData.Pickups.Count) return;
        tileX = Math.Clamp(tileX, 0, _mapData.Width - 1);
        tileY = Math.Clamp(tileY, 0, _mapData.Height - 1);
        RecordChange(index, pickup =>
        {
            pickup.TileX = tileX;
            pickup.TileY = tileY;
        });
    }

    public void SetAmount(int index, int amount)
    {
        if (index < 0 || index >= _mapData.Pickups.Count) return;
        RecordChange(index, pickup => pickup.Amount = Math.Max(0, amount));
    }

    public void SetType(int index, PickupType type)
    {
        if (index < 0 || index >= _mapData.Pickups.Count) return;
        RecordChange(index, pickup => pickup.Type = type);
        SelectedType = type;
    }

    public void ClearSelectionAndHover()
    {
        SelectedIndex = -1;
        HoveredIndex = -1;
        IsDragging = false;
    }

    private void RecordChange(int index, Action<PickupPlacement> apply)
    {
        var before = PickupPlacementData.FromPlacement(_mapData.Pickups[index]);
        apply(_mapData.Pickups[index]);
        var after = PickupPlacementData.FromPlacement(_mapData.Pickups[index]);
        _undoStack.Push(new ModifyPickupCommand(index, before, after));
        _notifyChanged();
    }
}
