using System.Numerics;

namespace Game.Utilities;

/// <summary>Finds light fixtures on <see cref="MapData.Objects"/> and converts them to world positions.</summary>
public static class TileLightCollector
{
    public static List<Vector3> Collect(MapData map)
    {
        var lights = new List<Vector3>();
        if (map.Objects.Length != map.Width * map.Height)
            return lights;

        for (int index = 0; index < map.Objects.Length; index++)
        {
            if (!LightObjectEncoding.IsLightObject(map.Objects[index]))
                continue;

            int tileX = index % map.Width;
            int tileY = index / map.Width;
            lights.Add(LevelData.GetTileAnchorWorld(tileX, tileY, LightObjectEncoding.WorldAnchorY));
        }

        return lights;
    }

    /// <summary>Picks up to <paramref name="maxCount"/> lights nearest the viewer (XZ).</summary>
    public static Vector3[] SelectNearest(IReadOnlyList<Vector3> lights, Vector3 viewerPosition, int maxCount)
    {
        if (lights.Count == 0 || maxCount <= 0)
            return Array.Empty<Vector3>();

        if (lights.Count <= maxCount)
            return lights.ToArray();

        var indices = new int[lights.Count];
        for (int i = 0; i < indices.Length; i++)
            indices[i] = i;

        Array.Sort(indices, (a, b) =>
        {
            float da = DistanceXZ(lights[a], viewerPosition);
            float db = DistanceXZ(lights[b], viewerPosition);
            return da.CompareTo(db);
        });

        var result = new Vector3[maxCount];
        for (int i = 0; i < maxCount; i++)
            result[i] = lights[indices[i]];

        return result;
    }

    private static float DistanceXZ(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
