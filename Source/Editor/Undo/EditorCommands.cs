using Game.Core.Level;
using Game.Features.Enemies;
using Game.Features.LevelProgress;
using Game.Features.Pickups;
using Game.Features.Players;

namespace Game.Editor.Undo;

public readonly record struct TileChange(
    int LayerIndex,
    int X,
    int Y,
    uint OldTileId,
    uint NewTileId,
    SecretWallPlacement? RemovedSecret);

public sealed class ReplaceMapDataCommand(string beforeJson, string afterJson, string description) : IEditorCommand
{
    public string Description { get; } = description;

    public void Undo(EditorState state) => state.RestoreMapFromJson(beforeJson);

    public void Redo(EditorState state) => state.RestoreMapFromJson(afterJson);
}

public sealed class PaintTilesCommand(List<TileChange> changes) : IEditorCommand
{
    public string Description => changes.Count == 1 ? "Paint tile" : $"Paint {changes.Count} tiles";

    public void Undo(EditorState state) => Apply(state, reverse: true);

    public void Redo(EditorState state) => Apply(state, reverse: false);

    private void Apply(EditorState state, bool reverse)
    {
        foreach (var change in changes)
        {
            var layer = state.Layers[change.LayerIndex];
            int idx = state.MapData.Width * change.Y + change.X;
            layer.Tiles[idx] = reverse ? change.OldTileId : change.NewTileId;

            if (change.RemovedSecret != null)
            {
                if (reverse)
                    state.MapData.SecretWalls.Add(CloneSecret(change.RemovedSecret));
                else
                    state.RemoveSecretWallAt(change.X, change.Y);
            }
        }
    }

    private static SecretWallPlacement CloneSecret(SecretWallPlacement s) => new()
    {
        TileX = s.TileX,
        TileY = s.TileY,
        Direction = s.Direction,
        TravelTiles = s.TravelTiles
    };
}

public sealed class AddEnemyCommand(int index, EnemyPlacementData placement) : IEditorCommand
{
    public string Description => "Place enemy";

    public void Undo(EditorState state)
    {
        state.MapData.Enemies.RemoveAt(index);
        state.DeselectEnemy();
    }

    public void Redo(EditorState state) => state.MapData.Enemies.Insert(index, placement.ToPlacement());
}

public sealed class RemoveEnemyCommand(int index, EnemyPlacementData placement) : IEditorCommand
{
    public string Description => "Delete enemy";

    public void Undo(EditorState state) => state.MapData.Enemies.Insert(index, placement.ToPlacement());

    public void Redo(EditorState state) => state.MapData.Enemies.RemoveAt(index);
}

public sealed class MoveEnemyCommand(int index, int fromX, int fromY, int toX, int toY) : IEditorCommand
{
    public string Description => "Move enemy";

    public void Undo(EditorState state)
    {
        var enemy = state.MapData.Enemies[index];
        enemy.TileX = fromX;
        enemy.TileY = fromY;
    }

    public void Redo(EditorState state)
    {
        var enemy = state.MapData.Enemies[index];
        enemy.TileX = toX;
        enemy.TileY = toY;
    }
}

public sealed class ModifyEnemyCommand(int index, EnemyPlacementData before, EnemyPlacementData after) : IEditorCommand
{
    public string Description => "Edit enemy";

    public void Undo(EditorState state) => Apply(state, before);

    public void Redo(EditorState state) => Apply(state, after);

    private void Apply(EditorState state, EnemyPlacementData data)
    {
        if (index < 0 || index >= state.MapData.Enemies.Count) return;
        var target = state.MapData.Enemies[index];
        target.TileX = data.TileX;
        target.TileY = data.TileY;
        target.Rotation = data.Rotation;
        target.EnemyType = data.EnemyType;
        target.StartsAsCorpse = data.StartsAsCorpse;
        target.DropsAmmo = data.DropsAmmo;
        target.PatrolPath = data.PatrolPath.Select(w => w.ToWaypoint()).ToList();
    }
}

public sealed class PlacePickupCommand(
    int index,
    PickupPlacementData placement,
    int? replacedIndex,
    PickupPlacementData? replacedPlacement) : IEditorCommand
{
    public string Description => "Place pickup";

    public void Undo(EditorState state)
    {
        state.MapData.Pickups.RemoveAt(index);
        if (replacedIndex.HasValue && replacedPlacement != null)
            state.MapData.Pickups.Insert(replacedIndex.Value, replacedPlacement.ToPlacement());
    }

    public void Redo(EditorState state)
    {
        if (replacedIndex.HasValue)
            state.MapData.Pickups.RemoveAt(replacedIndex.Value);
        state.MapData.Pickups.Insert(index, placement.ToPlacement());
    }
}

public sealed class RemovePickupCommand(int index, PickupPlacementData placement) : IEditorCommand
{
    public string Description => "Delete pickup";

    public void Undo(EditorState state) => state.MapData.Pickups.Insert(index, placement.ToPlacement());

    public void Redo(EditorState state) => state.MapData.Pickups.RemoveAt(index);
}

public sealed class MovePickupCommand(
    int index,
    int fromX,
    int fromY,
    int toX,
    int toY,
    int? removedIndex,
    PickupPlacementData? removedPlacement) : IEditorCommand
{
    public string Description => "Move pickup";

    public void Undo(EditorState state)
    {
        var pickup = state.MapData.Pickups[index];
        pickup.TileX = fromX;
        pickup.TileY = fromY;

        if (removedIndex.HasValue && removedPlacement != null)
            state.MapData.Pickups.Insert(removedIndex.Value, removedPlacement.ToPlacement());
    }

    public void Redo(EditorState state)
    {
        if (removedIndex.HasValue)
            state.MapData.Pickups.RemoveAt(removedIndex.Value);

        var pickup = state.MapData.Pickups[index];
        pickup.TileX = toX;
        pickup.TileY = toY;
    }
}

public sealed class ModifyPickupCommand(int index, PickupPlacementData before, PickupPlacementData after) : IEditorCommand
{
    public string Description => "Edit pickup";

    public void Undo(EditorState state) => Apply(state, before);

    public void Redo(EditorState state) => Apply(state, after);

    private void Apply(EditorState state, PickupPlacementData data)
    {
        if (index < 0 || index >= state.MapData.Pickups.Count) return;
        var target = state.MapData.Pickups[index];
        var placement = data.ToPlacement();
        target.TileX = placement.TileX;
        target.TileY = placement.TileY;
        target.Type = placement.Type;
        target.Amount = placement.Amount;
    }
}

public sealed class SetPlayerSpawnCommand(
    int oldTileX, int oldTileY, float oldRotation,
    int newTileX, int newTileY, float newRotation) : IEditorCommand
{
    public string Description => "Move player spawn";

    public void Undo(EditorState state) => Apply(state, oldTileX, oldTileY, oldRotation);

    public void Redo(EditorState state) => Apply(state, newTileX, newTileY, newRotation);

    private static void Apply(EditorState state, int tileX, int tileY, float rotation)
    {
        state.MapData.Spawn.TileX = tileX;
        state.MapData.Spawn.TileY = tileY;
        state.MapData.Spawn.Rotation = rotation;
        PlayerSpawn.ApplyFromMap(state.Player, state.MapData, PlayerSpawnApplyMode.PositionAndCameraOnly);
    }
}

public sealed class SetSecretWallCommand(
    int tileX,
    int tileY,
    SecretWallPlacement? before,
    SecretWallPlacement? after) : IEditorCommand
{
    public string Description => after != null ? "Set secret wall" : "Remove secret wall";

    public void Undo(EditorState state) => Apply(state, before);

    public void Redo(EditorState state) => Apply(state, after);

    private void Apply(EditorState state, SecretWallPlacement? secret)
    {
        state.RemoveSecretWallAt(tileX, tileY);
        if (secret != null)
        {
            state.MapData.SecretWalls.Add(new SecretWallPlacement
            {
                TileX = secret.TileX,
                TileY = secret.TileY,
                Direction = secret.Direction,
                TravelTiles = secret.TravelTiles
            });
        }
    }
}

public sealed class SetPatrolPathCommand(int enemyIndex, List<PatrolWaypoint> before, List<PatrolWaypoint> after) : IEditorCommand
{
    public string Description => "Edit patrol path";

    public void Undo(EditorState state) => Apply(state, before);

    public void Redo(EditorState state) => Apply(state, after);

    private void Apply(EditorState state, List<PatrolWaypoint> path)
    {
        if (enemyIndex < 0 || enemyIndex >= state.MapData.Enemies.Count) return;
        state.MapData.Enemies[enemyIndex].PatrolPath = path.Select(w => new PatrolWaypoint
        {
            TileX = w.TileX,
            TileY = w.TileY
        }).ToList();
    }
}
