using Game.Editor.Undo;
using Game.Features.LevelProgress;

namespace Game.Editor;

/// <summary>Wall selection and secret-wall placement edits for the level editor.</summary>
public sealed class SecretWallEditorTool
{
    private readonly MapData _mapData;
    private readonly EditorUndoStack _undoStack;
    private readonly Action _notifyChanged;
    private readonly Action _rebuildRoomMap;
    private readonly Action _deselectOthers;

    public int SelectedTileX { get; private set; } = -1;
    public int SelectedTileY { get; private set; } = -1;
    public bool HasSelection => SelectedTileX >= 0 && SelectedTileY >= 0;

    public SecretWallEditorTool(
        MapData mapData,
        EditorUndoStack undoStack,
        Action notifyChanged,
        Action rebuildRoomMap,
        Action deselectOthers)
    {
        _mapData = mapData;
        _undoStack = undoStack;
        _notifyChanged = notifyChanged;
        _rebuildRoomMap = rebuildRoomMap;
        _deselectOthers = deselectOthers;
    }

    public void SelectTile(int tileX, int tileY, Func<int, int, uint> getWallTile)
    {
        if (getWallTile(tileX, tileY) == 0) return;

        _deselectOthers();
        SelectedTileX = tileX;
        SelectedTileY = tileY;
        _notifyChanged();
    }

    public void ClearSelection()
    {
        if (SelectedTileX < 0) return;
        SelectedTileX = -1;
        SelectedTileY = -1;
        _notifyChanged();
    }

    public SecretWallPlacement? FindAt(int tileX, int tileY) =>
        _mapData.SecretWalls.FirstOrDefault(s => s.TileX == tileX && s.TileY == tileY);

    public SecretWallPlacement? GetSelectedPlacement() =>
        HasSelection ? FindAt(SelectedTileX, SelectedTileY) : null;

    public bool IsSelectedSecret => GetSelectedPlacement() != null;

    public void SetSecret(bool isSecret, SecretWallDirection direction, int travelTiles)
    {
        if (!HasSelection) return;

        var before = ClonePlacement(FindAt(SelectedTileX, SelectedTileY));
        RemoveAt(SelectedTileX, SelectedTileY);
        SecretWallPlacement? after = null;
        if (isSecret)
        {
            after = new SecretWallPlacement
            {
                TileX = SelectedTileX,
                TileY = SelectedTileY,
                Direction = direction,
                TravelTiles = ClampTravelTiles(SelectedTileX, SelectedTileY, direction, travelTiles)
            };
            _mapData.SecretWalls.Add(after);
        }

        if (!PlacementsEqual(before, after))
            _undoStack.Push(new SetSecretWallCommand(SelectedTileX, SelectedTileY, before, after));

        _rebuildRoomMap();
        _notifyChanged();
    }

    public void RemoveAt(int tileX, int tileY)
    {
        int index = _mapData.SecretWalls.FindIndex(s => s.TileX == tileX && s.TileY == tileY);
        if (index >= 0)
            _mapData.SecretWalls.RemoveAt(index);
    }

    public int GetMaxTravelTiles(int tileX, int tileY, SecretWallDirection direction)
    {
        var (dx, dy) = SecretWallDirectionHelper.ToTileDelta(direction);
        int count = 0;
        int x = tileX + dx;
        int y = tileY + dy;
        while (x >= 0 && x < _mapData.Width && y >= 0 && y < _mapData.Height)
        {
            count++;
            x += dx;
            y += dy;
        }

        return Math.Max(1, count);
    }

    public int ClampTravelTiles(int tileX, int tileY, SecretWallDirection direction, int travelTiles) =>
        Math.Clamp(travelTiles, 1, GetMaxTravelTiles(tileX, tileY, direction));

    private static SecretWallPlacement? ClonePlacement(SecretWallPlacement? secret) =>
        secret == null ? null : new SecretWallPlacement
        {
            TileX = secret.TileX,
            TileY = secret.TileY,
            Direction = secret.Direction,
            TravelTiles = secret.TravelTiles
        };

    private static bool PlacementsEqual(SecretWallPlacement? a, SecretWallPlacement? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.TileX == b.TileX
            && a.TileY == b.TileY
            && a.Direction == b.Direction
            && a.TravelTiles == b.TravelTiles;
    }
}
