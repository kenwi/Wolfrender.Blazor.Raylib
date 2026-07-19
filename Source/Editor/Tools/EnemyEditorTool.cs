using Game.Editor.Undo;
using Game.Features.Enemies;

namespace Game.Editor;

/// <summary>Enemy placement selection, drag, properties, and patrol-path editing.</summary>
public sealed class EnemyEditorTool
{
    private readonly MapData _mapData;
    private readonly EditorUndoStack _undoStack;
    private readonly Action _notifyChanged;
    private readonly Action _onPlacementEdited;
    private readonly Action _deselectOthers;

    private int _dragIndex = -1;
    private int _dragStartX;
    private int _dragStartY;

    public int HoveredIndex { get; set; } = -1;
    public int SelectedIndex { get; set; } = -1;
    public bool IsDragging { get; set; }

    public bool IsEditingPatrolPath { get; private set; }
    public int PatrolEditEnemyIndex { get; private set; } = -1;
    public List<PatrolWaypoint> PatrolPathInProgress { get; } = new();

    public EnemyEditorTool(
        MapData mapData,
        EditorUndoStack undoStack,
        Action notifyChanged,
        Action onPlacementEdited,
        Action deselectOthers)
    {
        _mapData = mapData;
        _undoStack = undoStack;
        _notifyChanged = notifyChanged;
        _onPlacementEdited = onPlacementEdited;
        _deselectOthers = deselectOthers;
    }

    public void Place(int x, int y)
    {
        if (x < 0 || x >= _mapData.Width || y < 0 || y >= _mapData.Height) return;
        var placement = new EnemyPlacement
        {
            TileX = x, TileY = y, Rotation = 0, EnemyType = "Guard"
        };
        _mapData.Enemies.Add(placement);
        int index = _mapData.Enemies.Count - 1;
        _undoStack.Push(new AddEnemyCommand(index, EnemyPlacementData.FromPlacement(placement)));
        _deselectOthers();
        SelectedIndex = index;
        IsDragging = true;
        BeginDrag();
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
        BeginDrag();
        _notifyChanged();
    }

    public void BeginDrag()
    {
        if (SelectedIndex < 0 || SelectedIndex >= _mapData.Enemies.Count) return;
        var enemy = _mapData.Enemies[SelectedIndex];
        _dragIndex = SelectedIndex;
        _dragStartX = enemy.TileX;
        _dragStartY = enemy.TileY;
    }

    public void EndDrag()
    {
        if (_dragIndex < 0 || _dragIndex >= _mapData.Enemies.Count)
        {
            _dragIndex = -1;
            return;
        }

        var enemy = _mapData.Enemies[_dragIndex];
        if (enemy.TileX != _dragStartX || enemy.TileY != _dragStartY)
        {
            _undoStack.Push(new MoveEnemyCommand(
                _dragIndex, _dragStartX, _dragStartY, enemy.TileX, enemy.TileY));
            _notifyChanged();
        }

        _dragIndex = -1;
    }

    public void MoveSelected(int x, int y)
    {
        if (SelectedIndex < 0 || SelectedIndex >= _mapData.Enemies.Count) return;
        if (x < 0 || x >= _mapData.Width || y < 0 || y >= _mapData.Height) return;
        _mapData.Enemies[SelectedIndex].TileX = x;
        _mapData.Enemies[SelectedIndex].TileY = y;
    }

    public void DeleteSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= _mapData.Enemies.Count) return;
        DeleteAt(SelectedIndex);
    }

    public void DeleteAt(int index)
    {
        if (index < 0 || index >= _mapData.Enemies.Count) return;
        var data = EnemyPlacementData.FromPlacement(_mapData.Enemies[index]);
        _mapData.Enemies.RemoveAt(index);
        _undoStack.Push(new RemoveEnemyCommand(index, data));
        if (SelectedIndex == index)
            SelectedIndex = -1;
        else if (SelectedIndex > index)
            SelectedIndex--;
        _notifyChanged();
    }

    public void SetTilePosition(int index, int tileX, int tileY)
    {
        if (index < 0 || index >= _mapData.Enemies.Count) return;
        tileX = Math.Clamp(tileX, 0, _mapData.Width - 1);
        tileY = Math.Clamp(tileY, 0, _mapData.Height - 1);
        RecordChange(index, enemy =>
        {
            enemy.TileX = tileX;
            enemy.TileY = tileY;
        });
    }

    public void SetRotation(int index, float rotation)
    {
        if (index < 0 || index >= _mapData.Enemies.Count) return;
        RecordChange(index, enemy => enemy.Rotation = rotation);
    }

    public void SetType(int index, string enemyType)
    {
        if (index < 0 || index >= _mapData.Enemies.Count) return;
        RecordChange(index, enemy => enemy.EnemyType = enemyType);
    }

    public void SetStartsAsCorpse(int index, bool value)
    {
        if (index < 0 || index >= _mapData.Enemies.Count) return;
        RecordChange(index, enemy => enemy.StartsAsCorpse = value);
    }

    public void SetDropsAmmo(int index, bool value)
    {
        if (index < 0 || index >= _mapData.Enemies.Count) return;
        RecordChange(index, enemy => enemy.DropsAmmo = value);
    }

    public void ClearPatrolPath(int index)
    {
        if (index < 0 || index >= _mapData.Enemies.Count) return;
        if (_mapData.Enemies[index].PatrolPath.Count == 0) return;
        RecordChange(index, enemy => enemy.PatrolPath.Clear());
    }

    public void AddPatrolWaypoint(int x, int y)
    {
        if (x < 0 || x >= _mapData.Width || y < 0 || y >= _mapData.Height) return;
        PatrolPathInProgress.Add(new PatrolWaypoint { TileX = x, TileY = y });
    }

    public void ConfirmPatrolPath()
    {
        if (PatrolEditEnemyIndex >= 0 && PatrolEditEnemyIndex < _mapData.Enemies.Count)
        {
            var enemy = _mapData.Enemies[PatrolEditEnemyIndex];
            var before = enemy.PatrolPath.Select(w => new PatrolWaypoint { TileX = w.TileX, TileY = w.TileY }).ToList();
            var after = PatrolPathInProgress.Select(w => new PatrolWaypoint { TileX = w.TileX, TileY = w.TileY }).ToList();
            enemy.PatrolPath = new List<PatrolWaypoint>(after);
            _undoStack.Push(new SetPatrolPathCommand(PatrolEditEnemyIndex, before, after));
        }
        IsEditingPatrolPath = false;
        PatrolPathInProgress.Clear();
        PatrolEditEnemyIndex = -1;
        _notifyChanged();
    }

    public void CancelPatrolPath()
    {
        IsEditingPatrolPath = false;
        PatrolPathInProgress.Clear();
        PatrolEditEnemyIndex = -1;
        _notifyChanged();
    }

    public void StartEditingPatrolPath()
    {
        if (SelectedIndex < 0) return;
        IsEditingPatrolPath = true;
        PatrolEditEnemyIndex = SelectedIndex;
        PatrolPathInProgress.Clear();
        _notifyChanged();
    }

    public void ClearSelectionAndHover()
    {
        SelectedIndex = -1;
        HoveredIndex = -1;
        IsDragging = false;
    }

    private void RecordChange(int index, Action<EnemyPlacement> apply)
    {
        var before = EnemyPlacementData.FromPlacement(_mapData.Enemies[index]);
        apply(_mapData.Enemies[index]);
        var after = EnemyPlacementData.FromPlacement(_mapData.Enemies[index]);
        _undoStack.Push(new ModifyEnemyCommand(index, before, after));
        _onPlacementEdited();
    }
}
