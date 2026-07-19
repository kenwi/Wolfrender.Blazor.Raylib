using System.Numerics;
using Game.Engine.Rendering;

namespace Game.Features.WorldObjects;

/// <summary>
/// Shared CPU→GPU lighting prep for the 3D scene and lighting diagnostics.
/// Updates occlusion, binds room/occlusion maps, and selects active tile lights.
/// </summary>
public static class SceneLightingSetup
{
    public static void ApplyForView(
        MapData mapData,
        LightOcclusionMap occlusionMap,
        LevelRoomMap roomMap,
        IDoorTileEncoding doorEncoding,
        IDoorPortalState doors,
        Vector3 viewPosition,
        Func<Vector3, IDoorPortalState, HashSet<int>> computeVisibleRooms)
    {
        occlusionMap.Update(mapData, doorEncoding, doors, roomMap);
        PrimitiveRenderer.SetLightOcclusionMap(occlusionMap, mapData.Width, mapData.Height);
        PrimitiveRenderer.SetSpriteRoomMap(roomMap);

        var mapLights = TileLightCollector.Collect(mapData);
        var visibleRooms = computeVisibleRooms(viewPosition, doors);
        var activeTileLights = TileLightCollector.SelectForVisibleRooms(
            mapLights,
            roomMap,
            visibleRooms,
            viewPosition,
            LightObjectEncoding.MaxShaderLights);
        PrimitiveRenderer.SetLightingParameters(viewPosition, tileLights: activeTileLights);
    }
}
