using Game.Engine.Rendering;
using Game.Features.Doors;
using Game.Features.WorldObjects;

namespace Game.Editor;

/// <summary>
/// Tile ID rules for editor palettes and painting. Door-encoded IDs (7–12, 57–68) belong on the Doors layer only.
/// </summary>
public static class EditorTilePalette
{
    public static bool IsArchitecturalLayer(string layerName) =>
        layerName is "Floor" or "Walls" or "Ceiling";

    public static bool IsArchitecturalTileId(uint tileId) =>
        tileId > 0
        && tileId <= TileSpriteSheet.TileCount
        && !DoorTileEncoding.IsDoorTile(tileId);

    public static bool IsTileIdValidForLayer(uint tileId, string layerName)
    {
        if (tileId == 0) return true;
        if (layerName == EditorState.DoorsLayerName)
            return DoorTileEncoding.IsDoorTile(tileId);
        if (layerName == EditorState.ObjectsLayerName)
            return ObjectSprites.IsValidObjectId(tileId);
        if (IsArchitecturalLayer(layerName))
            return IsArchitecturalTileId(tileId);
        return false;
    }

    public static uint GetDefaultTileIdForLayer(string layerName) =>
        layerName == EditorState.DoorsLayerName
            ? DoorTileEncoding.LightHorizontal
            : 1;

    public static void SanitizeSelectedTile(EditorState state)
    {
        if (IsTileIdValidForLayer(state.SelectedTileId, state.ActiveLayer.Name))
            return;

        state.SelectedTileId = GetDefaultTileIdForLayer(state.ActiveLayer.Name);
    }
}
