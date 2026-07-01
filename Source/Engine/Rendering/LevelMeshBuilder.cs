using System.Numerics;
using Game.Core.Level;
using Game.Features.Doors;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Engine.Rendering;

/// <summary>
/// Bakes static wall/floor/ceiling geometry from <see cref="MapData"/> with neighbor face culling.
/// Walls, floors, and ceilings are grouped per room for visibility filtering at draw time.
/// </summary>
internal static class LevelMeshBuilder
{
    private const float TileSize = 4f;
    private const float WallCenterY = 2f;
    private const float FloorCenterY = -2f;
    private const float CeilingCenterY = 6f;

    /// <summary>Stay below the 16-bit index limit when uploading a single mesh.</summary>
    private const int MaxVerticesPerMesh = 60_000;

    internal sealed class MeshBatch
    {
        public required Mesh Mesh { get; init; }
        public required int TextureIndex { get; init; }
        public required LevelMeshLayer Layer { get; init; }
        public required int QuadCount { get; init; }
        /// <summary>Room id for baked batches; -1 for unassigned geometry.</summary>
        public int RoomId { get; init; } = -1;
    }

    public static List<MeshBatch> Build(MapData mapData)
    {
        int width = mapData.Width;
        int height = mapData.Height;
        int maxTextureIndex = mapData.TileTextures.Count;
        var roomMap = LevelRoomMap.Build(mapData);

        var wallBuilders = new Dictionary<int, MeshGeometryBuilder[]>();
        var floorBuilders = new Dictionary<int, MeshGeometryBuilder[]>();
        var ceilingBuilders = new Dictionary<int, MeshGeometryBuilder[]>();
        var secretTiles = BuildSecretTileSet(mapData);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = LevelData.GetIndex(x, y, width);
                bool hasDoor = mapData.Doors[index] != 0 && DoorTileEncoding.IsDoorTile(mapData.Doors[index]);

                uint wallTile = mapData.Walls[index];
                if (wallTile != 0 && !hasDoor)
                    AddWallTile(wallBuilders, roomMap, mapData, x, y, (int)wallTile - 1, maxTextureIndex);

                if (!ShouldEmitFloorOrCeiling(mapData, x, y, hasDoor, secretTiles))
                    continue;

                var roomIds = roomMap.GetTileRoomIds(x, y);
                uint floorTile = mapData.Floor[index];
                if (floorTile != 0)
                {
                    foreach (int roomId in roomIds)
                        AddFloorTile(GetRoomBuilders(floorBuilders, roomId, maxTextureIndex), (int)floorTile - 1, x, y);
                }

                uint ceilingTile = mapData.Ceiling[index];
                if (ceilingTile != 0)
                {
                    foreach (int roomId in roomIds)
                        AddCeilingTile(GetRoomBuilders(ceilingBuilders, roomId, maxTextureIndex), (int)ceilingTile - 1, x, y);
                }
            }
        }

        var batches = new List<MeshBatch>();
        AppendRoomBatches(batches, wallBuilders, LevelMeshLayer.Walls);
        AppendRoomBatches(batches, floorBuilders, LevelMeshLayer.Floors);
        AppendRoomBatches(batches, ceilingBuilders, LevelMeshLayer.Ceilings);
        return batches;
    }

    private static MeshGeometryBuilder[] GetRoomBuilders(
        Dictionary<int, MeshGeometryBuilder[]> buildersByRoom,
        int roomId,
        int textureCount)
    {
        if (!buildersByRoom.TryGetValue(roomId, out var builders))
        {
            builders = CreateBuilders(textureCount);
            buildersByRoom[roomId] = builders;
        }

        return builders;
    }

    /// <summary>
    /// Floor/ceiling quads are omitted on wall tiles unless the tile is a door or secret wall.
    /// </summary>
    internal static bool ShouldEmitFloorOrCeiling(
        MapData mapData,
        int x,
        int y,
        bool hasDoor,
        IReadOnlySet<(int x, int y)> secretTiles)
    {
        int index = LevelData.GetIndex(x, y, mapData.Width);
        if (mapData.Walls[index] == 0)
            return true;

        if (hasDoor || secretTiles.Contains((x, y)))
            return true;

        return false;
    }

    private static HashSet<(int x, int y)> BuildSecretTileSet(MapData mapData)
    {
        var secretTiles = new HashSet<(int x, int y)>();
        foreach (var secret in mapData.SecretWalls)
            secretTiles.Add((secret.TileX, secret.TileY));
        return secretTiles;
    }

    private static MeshGeometryBuilder[] CreateBuilders(int textureCount)
    {
        var builders = new MeshGeometryBuilder[Math.Max(textureCount, 1)];
        for (int i = 0; i < builders.Length; i++)
            builders[i] = new MeshGeometryBuilder();
        return builders;
    }

    private static void AppendRoomBatches(
        List<MeshBatch> batches,
        Dictionary<int, MeshGeometryBuilder[]> buildersByRoom,
        LevelMeshLayer layer)
    {
        foreach (var (roomId, builders) in buildersByRoom.OrderBy(pair => pair.Key))
        {
            for (int textureIndex = 0; textureIndex < builders.Length; textureIndex++)
            {
                foreach (var (mesh, quadCount) in builders[textureIndex].BuildMeshes())
                {
                    if (mesh.VertexCount > 0)
                    {
                        batches.Add(new MeshBatch
                        {
                            Mesh = mesh,
                            TextureIndex = textureIndex,
                            Layer = layer,
                            QuadCount = quadCount,
                            RoomId = roomId
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// True when no wall face should be emitted toward neighbor (x, y):
    /// solid wall, map out-of-bounds, or unused padding tiles (map void).
    /// </summary>
    private static bool BlocksWallFace(MapData mapData, int x, int y)
    {
        if (x < 0 || x >= mapData.Width || y < 0 || y >= mapData.Height)
            return true;

        int index = LevelData.GetIndex(x, y, mapData.Width);
        if (mapData.Walls[index] != 0)
            return true;

        // Playable open tiles (and doorways) still need the adjacent wall face.
        if (mapData.Floor[index] != 0 || mapData.Ceiling[index] != 0)
            return false;

        if (mapData.Doors[index] != 0 && DoorTileEncoding.IsDoorTile(mapData.Doors[index]))
            return false;

        // In-bounds map void (no layers) - exterior shell not visible in play.
        return true;
    }

    private static void AddWallTile(
        Dictionary<int, MeshGeometryBuilder[]> buildersByRoom,
        LevelRoomMap roomMap,
        MapData mapData,
        int x,
        int y,
        int textureIndex,
        int maxTextureIndex)
    {
        if (textureIndex < 0)
            return;

        float cx = x * TileSize;
        float cy = WallCenterY;
        float cz = y * TileSize;
        float half = TileSize * 0.5f;

        TryAddWallFace(buildersByRoom, roomMap, mapData, x, y, textureIndex, maxTextureIndex, x, y + 1,
            new Vector3(cx - half, cy - half, cz + half),
            new Vector3(cx + half, cy - half, cz + half),
            new Vector3(cx + half, cy + half, cz + half),
            new Vector3(cx - half, cy + half, cz + half),
            new Vector3(0f, 0f, 1f),
            T(0f, 0f), T(1f, 0f), T(1f, 1f), T(0f, 1f));

        TryAddWallFace(buildersByRoom, roomMap, mapData, x, y, textureIndex, maxTextureIndex, x, y - 1,
            new Vector3(cx - half, cy - half, cz - half),
            new Vector3(cx - half, cy + half, cz - half),
            new Vector3(cx + half, cy + half, cz - half),
            new Vector3(cx + half, cy - half, cz - half),
            new Vector3(0f, 0f, -1f),
            T(1f, 0f), T(1f, 1f), T(0f, 1f), T(0f, 0f));

        TryAddWallFace(buildersByRoom, roomMap, mapData, x, y, textureIndex, maxTextureIndex, x + 1, y,
            new Vector3(cx + half, cy - half, cz - half),
            new Vector3(cx + half, cy + half, cz - half),
            new Vector3(cx + half, cy + half, cz + half),
            new Vector3(cx + half, cy - half, cz + half),
            new Vector3(1f, 0f, 0f),
            T(1f, 0f), T(1f, 1f), T(0f, 1f), T(0f, 0f));

        TryAddWallFace(buildersByRoom, roomMap, mapData, x, y, textureIndex, maxTextureIndex, x - 1, y,
            new Vector3(cx - half, cy - half, cz - half),
            new Vector3(cx - half, cy - half, cz + half),
            new Vector3(cx - half, cy + half, cz + half),
            new Vector3(cx - half, cy + half, cz - half),
            new Vector3(-1f, 0f, 0f),
            T(0f, 0f), T(1f, 0f), T(1f, 1f), T(0f, 1f));
    }

    /// <summary>
    /// Emit a wall face toward walkable neighbor (nx, ny) into the room(s) on that side of the wall.
    /// </summary>
    private static void TryAddWallFace(
        Dictionary<int, MeshGeometryBuilder[]> buildersByRoom,
        LevelRoomMap roomMap,
        MapData mapData,
        int x,
        int y,
        int textureIndex,
        int maxTextureIndex,
        int neighborX,
        int neighborY,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        Vector3 v3,
        Vector3 normal,
        Vector2 t0,
        Vector2 t1,
        Vector2 t2,
        Vector2 t3)
    {
        if (BlocksWallFace(mapData, neighborX, neighborY))
            return;

        foreach (int roomId in roomMap.GetTileRoomIds(neighborX, neighborY))
        {
            if (roomId < 0)
                continue;

            var builders = GetRoomBuilders(buildersByRoom, roomId, maxTextureIndex);
            if (textureIndex >= builders.Length)
                continue;

            builders[textureIndex].AddQuad(v0, v1, v2, v3, normal, t0, t1, t2, t3);
        }
    }

    private static void AddFloorTile(MeshGeometryBuilder[] builders, int textureIndex, int x, int y)
    {
        if (textureIndex < 0 || textureIndex >= builders.Length)
            return;

        float cx = x * TileSize;
        float cy = FloorCenterY;
        float cz = y * TileSize;
        float half = TileSize * 0.5f;
        float top = cy + half;

        builders[textureIndex].AddQuad(
            new Vector3(cx - half, top, cz - half),
            new Vector3(cx - half, top, cz + half),
            new Vector3(cx + half, top, cz + half),
            new Vector3(cx + half, top, cz - half),
            new Vector3(0f, 1f, 0f),
            T(0f, 1f), T(0f, 0f), T(1f, 0f), T(1f, 1f));
    }

    private static void AddCeilingTile(MeshGeometryBuilder[] builders, int textureIndex, int x, int y)
    {
        if (textureIndex < 0 || textureIndex >= builders.Length)
            return;

        float cx = x * TileSize;
        float cy = CeilingCenterY;
        float cz = y * TileSize;
        float half = TileSize * 0.5f;
        float bottom = cy - half;

        builders[textureIndex].AddQuad(
            new Vector3(cx - half, bottom, cz - half),
            new Vector3(cx + half, bottom, cz - half),
            new Vector3(cx + half, bottom, cz + half),
            new Vector3(cx - half, bottom, cz + half),
            new Vector3(0f, -1f, 0f),
            T(1f, 1f), T(0f, 1f), T(0f, 0f), T(1f, 0f));
    }

    private static Vector2 T(float u, float v) => new(u, 1f - v);

    private sealed class MeshGeometryBuilder
    {
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector2> _texcoords = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<ushort> _indices = new();
        private readonly List<(Mesh Mesh, int QuadCount)> _completed = new();
        private int _quadCount;

        public void AddQuad(
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
            Vector3 normal,
            Vector2 t0, Vector2 t1, Vector2 t2, Vector2 t3)
        {
            if (_vertices.Count + 4 > MaxVerticesPerMesh)
                FlushCurrent();

            ushort baseIdx = (ushort)_vertices.Count;
            _vertices.Add(v0);
            _vertices.Add(v1);
            _vertices.Add(v2);
            _vertices.Add(v3);
            _texcoords.Add(t0);
            _texcoords.Add(t1);
            _texcoords.Add(t2);
            _texcoords.Add(t3);
            _normals.Add(normal);
            _normals.Add(normal);
            _normals.Add(normal);
            _normals.Add(normal);
            _indices.Add(baseIdx);
            _indices.Add((ushort)(baseIdx + 1));
            _indices.Add((ushort)(baseIdx + 2));
            _indices.Add(baseIdx);
            _indices.Add((ushort)(baseIdx + 2));
            _indices.Add((ushort)(baseIdx + 3));
            _quadCount++;
        }

        public IEnumerable<(Mesh Mesh, int QuadCount)> BuildMeshes()
        {
            FlushCurrent();
            return _completed;
        }

        private void FlushCurrent()
        {
            if (_vertices.Count == 0)
                return;

            int triangleCount = _indices.Count / 3;
            var mesh = new Mesh(_vertices.Count, triangleCount);
            mesh.AllocVertices();
            mesh.AllocTexCoords();
            mesh.AllocNormals();
            mesh.AllocIndices();

            var vertices = mesh.VerticesAs<Vector3>();
            var texcoords = mesh.TexCoordsAs<Vector2>();
            var normals = mesh.NormalsAs<Vector3>();
            var indices = mesh.IndicesAs<ushort>();

            for (int i = 0; i < _vertices.Count; i++)
            {
                vertices[i] = _vertices[i];
                texcoords[i] = _texcoords[i];
                normals[i] = _normals[i];
            }

            for (int i = 0; i < _indices.Count; i++)
                indices[i] = _indices[i];

            UploadMesh(ref mesh, false);
            _completed.Add((mesh, _quadCount));

            _vertices.Clear();
            _texcoords.Clear();
            _normals.Clear();
            _indices.Clear();
            _quadCount = 0;
        }
    }
}
