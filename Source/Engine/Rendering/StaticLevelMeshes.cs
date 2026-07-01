using System.Numerics;
using Game.Core.Level;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Engine.Rendering;

/// <summary>
/// GPU-resident baked level geometry. Rebuilt when <see cref="MapData.GeometryVersion"/> changes.
/// </summary>
public sealed class StaticLevelMeshes : IDisposable
{
    private readonly List<LevelMeshBuilder.MeshBatch> _batches = new();
    private Material _drawMaterial;
    private bool _hasDrawMaterial;
    private int _bakedQuadCount;
    private int _trackedGeometryVersion = -1;

    public int BakedQuadCount => _bakedQuadCount;
    public bool HasMeshes => _batches.Count > 0;

    public void RebuildIfNeeded(MapData mapData, IReadOnlyList<Texture2D> textures)
    {
        if (mapData.GeometryVersion == _trackedGeometryVersion)
            return;

        Rebuild(mapData, textures);
    }

    public void Rebuild(MapData mapData, IReadOnlyList<Texture2D> textures)
    {
        UnloadMeshes();
        _batches.Clear();
        _bakedQuadCount = 0;

        foreach (var batch in LevelMeshBuilder.Build(mapData))
        {
            if (batch.TextureIndex < 0 || batch.TextureIndex >= textures.Count)
            {
                UnloadMesh(batch.Mesh);
                continue;
            }

            _batches.Add(batch);
            _bakedQuadCount += batch.QuadCount;
        }

        _trackedGeometryVersion = mapData.GeometryVersion;
    }

    public void Draw(
        IReadOnlyList<Texture2D> textures,
        Shader lightingShader,
        LevelMeshLayer? layerFilter = null,
        IReadOnlySet<int>? visibleRooms = null)
    {
        if (_batches.Count == 0)
            return;

        EnsureDrawMaterial(lightingShader);

        foreach (var batch in _batches)
        {
            if (layerFilter.HasValue && batch.Layer != layerFilter.Value)
                continue;

            if (!ShouldDrawBatch(batch, visibleRooms))
                continue;

            if (batch.TextureIndex < 0 || batch.TextureIndex >= textures.Count)
                continue;

            var texture = textures[batch.TextureIndex];
            SetMaterialTexture(ref _drawMaterial, MaterialMapIndex.Albedo, texture);
            DrawMesh(batch.Mesh, _drawMaterial, Matrix4x4.Identity);
        }
    }

    public int CountQuads(LevelMeshLayer? layerFilter = null, IReadOnlySet<int>? visibleRooms = null)
    {
        int count = 0;
        foreach (var batch in _batches)
        {
            if (layerFilter.HasValue && batch.Layer != layerFilter.Value)
                continue;

            if (!ShouldDrawBatch(batch, visibleRooms))
                continue;

            count += batch.QuadCount;
        }
        return count;
    }

    private static bool ShouldDrawBatch(LevelMeshBuilder.MeshBatch batch, IReadOnlySet<int>? visibleRooms)
    {
        if (visibleRooms == null)
            return true;

        if (batch.RoomId < 0)
            return true;

        return visibleRooms.Contains(batch.RoomId);
    }

    public void Dispose()
    {
        UnloadMeshes();
        _hasDrawMaterial = false;
    }

    private void EnsureDrawMaterial(Shader lightingShader)
    {
        if (!_hasDrawMaterial)
        {
            _drawMaterial = LoadMaterialDefault();
            _drawMaterial.Shader = lightingShader;
            _hasDrawMaterial = true;
            return;
        }

        if (_drawMaterial.Shader.Id != lightingShader.Id)
            _drawMaterial.Shader = lightingShader;
    }

    private void UnloadMeshes()
    {
        foreach (var batch in _batches)
            UnloadMesh(batch.Mesh);
    }
}
