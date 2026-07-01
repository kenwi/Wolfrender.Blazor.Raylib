using System.Numerics;
using Game.Engine.Rendering;
using ActiveTileLight = Game.Engine.Rendering.ActiveTileLight;

namespace Game.Features.WorldObjects;

/// <summary>Finds light fixtures on <see cref="MapData.Objects"/> and converts them to world positions.</summary>
public static class TileLightCollector
{
    public readonly record struct TileLight(int TileX, int TileY, Vector3 WorldPosition);

    public static List<TileLight> Collect(MapData map)
    {
        var lights = new List<TileLight>();
        if (map.Objects.Length != map.Width * map.Height)
            return lights;

        for (int index = 0; index < map.Objects.Length; index++)
        {
            if (!LightObjectEncoding.IsLightObject(map.Objects[index]))
                continue;

            int tileX = index % map.Width;
            int tileY = index / map.Width;
            lights.Add(new TileLight(
                tileX,
                tileY,
                LevelData.GetTileAnchorWorld(tileX, tileY, LightObjectEncoding.WorldAnchorY)));
        }

        return lights;
    }

    /// <summary>
    /// Picks up to <paramref name="maxCount"/> lights in visible rooms, nearest the viewer (XZ).
    /// </summary>
    public static ActiveTileLight[] SelectForVisibleRooms(
        IReadOnlyList<TileLight> lights,
        LevelRoomMap roomMap,
        IReadOnlySet<int> visibleRooms,
        Vector3 viewerPosition,
        int maxCount)
    {
        if (lights.Count == 0 || maxCount <= 0 || visibleRooms.Count == 0)
            return Array.Empty<ActiveTileLight>();

        var candidates = new List<TileLight>();
        foreach (var light in lights)
        {
            if (IsLightInVisibleRooms(roomMap, visibleRooms, light.TileX, light.TileY))
                candidates.Add(light);
        }

        return SelectNearest(candidates, roomMap, viewerPosition, maxCount);
    }

    private static bool IsLightInVisibleRooms(
        LevelRoomMap roomMap,
        IReadOnlySet<int> visibleRooms,
        int tileX,
        int tileY)
    {
        foreach (int roomId in roomMap.GetTileRoomIds(tileX, tileY))
        {
            if (roomId >= 0 && visibleRooms.Contains(roomId))
                return true;
        }

        return false;
    }

    public static ActiveTileLight[] SelectNearest(
        IReadOnlyList<TileLight> lights,
        LevelRoomMap roomMap,
        Vector3 viewerPosition,
        int maxCount)
    {
        if (lights.Count == 0 || maxCount <= 0)
            return Array.Empty<ActiveTileLight>();

        if (lights.Count <= maxCount)
        {
            var all = new ActiveTileLight[lights.Count];
            for (int i = 0; i < lights.Count; i++)
                all[i] = ToActive(lights[i], roomMap);
            return all;
        }

        var indices = new int[lights.Count];
        for (int i = 0; i < indices.Length; i++)
            indices[i] = i;

        Array.Sort(indices, (a, b) =>
        {
            float da = DistanceXZ(lights[a].WorldPosition, viewerPosition);
            float db = DistanceXZ(lights[b].WorldPosition, viewerPosition);
            return da.CompareTo(db);
        });

        var result = new ActiveTileLight[maxCount];
        for (int i = 0; i < maxCount; i++)
            result[i] = ToActive(lights[indices[i]], roomMap);

        return result;
    }

    private static ActiveTileLight ToActive(TileLight light, LevelRoomMap roomMap)
    {
        var roomIds = roomMap.GetTileRoomIds(light.TileX, light.TileY);
        int roomA = roomIds.Count > 0 ? roomIds[0] : -1;
        int roomB = roomIds.Count > 1 ? roomIds[1] : -1;
        return new ActiveTileLight(light.WorldPosition, roomA, roomB);
    }

    private static float DistanceXZ(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
