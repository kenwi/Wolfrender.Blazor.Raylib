using System.Numerics;
using Game.Engine.Rendering;

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
    public static Vector3[] SelectForVisibleRooms(
        IReadOnlyList<TileLight> lights,
        LevelRoomMap roomMap,
        IReadOnlySet<int> visibleRooms,
        Vector3 viewerPosition,
        int maxCount)
    {
        if (lights.Count == 0 || maxCount <= 0 || visibleRooms.Count == 0)
            return Array.Empty<Vector3>();

        var candidates = new List<TileLight>();
        foreach (var light in lights)
        {
            if (IsLightInVisibleRooms(roomMap, visibleRooms, light.TileX, light.TileY))
                candidates.Add(light);
        }

        return SelectNearest(candidates, viewerPosition, maxCount);
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

    /// <summary>Picks up to <paramref name="maxCount"/> lights nearest the viewer (XZ).</summary>
    public static Vector3[] SelectNearest(IReadOnlyList<TileLight> lights, Vector3 viewerPosition, int maxCount)
    {
        if (lights.Count == 0 || maxCount <= 0)
            return Array.Empty<Vector3>();

        if (lights.Count <= maxCount)
        {
            var all = new Vector3[lights.Count];
            for (int i = 0; i < lights.Count; i++)
                all[i] = lights[i].WorldPosition;
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

        var result = new Vector3[maxCount];
        for (int i = 0; i < maxCount; i++)
            result[i] = lights[indices[i]].WorldPosition;

        return result;
    }

    private static float DistanceXZ(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
