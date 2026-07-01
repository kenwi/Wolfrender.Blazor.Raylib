using System.Collections.Generic;
using System.Numerics;
using Game.Core.Level;
using Game.Features.Doors;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Engine.Rendering;

public class RenderSystem : IDisposable
{
    private readonly LevelData _level;
    private readonly MapData _mapData;
    private readonly List<Texture2D> _textures;
    private readonly float _drawDistance;
    private readonly StaticLevelMeshes _staticMeshes = new();
    private LevelRoomMap _roomMap;
    private HashSet<(int x, int y)> _secretTiles = new();
    private const float TileSize = 4.0f;
    /// <summary>Extra half-angle beyond the view frustum (projection / pitch slack).</summary>
    private const float FrustumEdgeMarginDegrees = 5f;

    // Track rendered tiles for minimap
    private HashSet<(int x, int y)> _renderedTiles = new HashSet<(int x, int y)>();

    public HashSet<(int x, int y)> RenderedTiles => _renderedTiles;

    /// <summary>
    /// When true, draw baked static meshes instead of immediate-mode quads.
    /// Toggle off to compare the legacy path on this branch.
    /// </summary>
    public bool UseStaticMeshes { get; set; } = true;

    public int BakedQuadCount => _staticMeshes.BakedQuadCount;

    public HashSet<int> ComputeVisibleRooms(Vector3 playerPosition, IReadOnlyList<Door> doors)
    {
        var (playerTileX, playerTileY) = LevelData.GetEntityTileFromWorld(playerPosition.X, playerPosition.Z);
        return _roomMap.ComputeVisibleRooms(playerTileX, playerTileY, doors);
    }

    public LevelRoomMap RoomMap => _roomMap;

    public RenderSystem(LevelData level, MapData mapData, List<Texture2D> textures, float drawDistance = 15.0f)
    {
        _level = level;
        _mapData = mapData;
        _textures = textures;
        _drawDistance = drawDistance;
        _roomMap = LevelRoomMap.Build(mapData);
        _staticMeshes.Rebuild(mapData, textures);
        RebuildSecretTileSet();
    }

    public void RebuildMeshes()
    {
        _roomMap = LevelRoomMap.Build(_mapData);
        _staticMeshes.Rebuild(_mapData, _textures);
        RebuildSecretTileSet();
    }

    private void RebuildSecretTileSet()
    {
        _secretTiles.Clear();
        foreach (var secret in _mapData.SecretWalls)
            _secretTiles.Add((secret.TileX, secret.TileY));
    }

    public void Render(Camera3D camera, int viewportWidth, int viewportHeight, IReadOnlyList<Door> doors)
    {
        LevelData.DrawedQuads = 0;
        _renderedTiles.Clear();

        _staticMeshes.RebuildIfNeeded(_mapData, _textures);

        if (UseStaticMeshes && _staticMeshes.HasMeshes)
        {
            DrawStaticMeshes(camera, doors);
            return;
        }

        RenderLegacyTiles(camera, viewportWidth, viewportHeight);
    }

    private void DrawStaticMeshes(Camera3D camera, IReadOnlyList<Door> doors)
    {
        var lightingShader = PrimitiveRenderer.GetLightingShader();
        if (!lightingShader.HasValue)
            return;

        var (playerTileX, playerTileY) = LevelData.GetEntityTileFromWorld(camera.Position.X, camera.Position.Z);
        var visibleRooms = _roomMap.ComputeVisibleRooms(playerTileX, playerTileY, doors);
        TrackVisibleRoomTiles(visibleRooms);

        _staticMeshes.Draw(_textures, lightingShader.Value, visibleRooms: visibleRooms);
        LevelData.DrawedQuads = _staticMeshes.CountQuads(visibleRooms: visibleRooms);
    }

    private void TrackVisibleRoomTiles(HashSet<int> visibleRooms)
    {
        foreach (var (x, y, roomId) in _roomMap.EnumerateRoomTiles())
        {
            if (visibleRooms.Contains(roomId))
                _renderedTiles.Add((x, y));
        }

        foreach (var link in _roomMap.DoorLinks)
        {
            if (visibleRooms.Contains(link.RoomA) || visibleRooms.Contains(link.RoomB))
                _renderedTiles.Add((link.DoorTileX, link.DoorTileY));
        }
    }

    private void RenderFloorAndCeiling(int x, int y, Vector3 worldPos)
    {
        int index = LevelData.GetIndex(x, y, _mapData.Width);
        bool hasDoor = _mapData.Doors[index] != 0 && DoorTileEncoding.IsDoorTile(_mapData.Doors[index]);
        if (!LevelMeshBuilder.ShouldEmitFloorOrCeiling(_mapData, x, y, hasDoor, _secretTiles))
            return;

        var floorTile = _level.GetFloorTile(x, y);
        if (floorTile != 0 && floorTile <= _textures.Count)
        {
            PrimitiveRenderer.DrawFloorTexture(
                _textures[(int)floorTile - 1],
                new Vector3(worldPos.X, -2, worldPos.Z),
                4.0f, 4.0f, 4.0f,
                Color.White
            );
        }

        var ceilingTile = _level.GetCeilingTile(x, y);
        if (ceilingTile != 0 && ceilingTile <= _textures.Count)
        {
            PrimitiveRenderer.DrawCeilingTexture(
                _textures[(int)ceilingTile - 1],
                new Vector3(worldPos.X, 6f, worldPos.Z),
                4.0f, 4.0f, 4.0f,
                Color.White
            );
        }
    }

    private void TrackVisibleTiles(Camera3D camera, int viewportWidth, int viewportHeight)
    {
        float aspect = viewportHeight > 0 ? viewportWidth / (float)viewportHeight : 1f;
        float halfHorizRad = ComputeHorizHalfFovRad(camera, aspect);
        float baseMarginRad = FrustumEdgeMarginDegrees * (MathF.PI / 180f);

        Vector3 cameraForward = Vector3.Normalize(camera.Target - camera.Position);
        Vector3 cameraForwardXZ = new Vector3(cameraForward.X, 0f, cameraForward.Z);
        if (cameraForwardXZ.LengthSquared() > 0.0001f)
            cameraForwardXZ = Vector3.Normalize(cameraForwardXZ);
        else
            cameraForwardXZ = new Vector3(0f, 0f, 1f);

        Vector3 cameraPosXZ = new Vector3(camera.Position.X, 0, camera.Position.Z);
        float drawDistanceWorld = _drawDistance * TileSize;

        int cameraTileX = (int)(camera.Position.X / TileSize + 0.5f);
        int cameraTileY = (int)(camera.Position.Z / TileSize + 0.5f);
        int minX = Math.Max(0, cameraTileX - (int)_drawDistance);
        int maxX = Math.Min(_level.Width - 1, cameraTileX + (int)_drawDistance);
        int minY = Math.Max(0, cameraTileY - (int)_drawDistance);
        int maxY = Math.Min(_level.Height - 1, cameraTileY + (int)_drawDistance);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector3 tilePos = new Vector3(x * TileSize, 0, y * TileSize);
                Vector3 toTile = tilePos - cameraPosXZ;
                float distance = toTile.Length();

                if (distance > drawDistanceWorld)
                    continue;

                Vector3 toTileNormalized = Vector3.Normalize(toTile);
                float dot = Vector3.Dot(cameraForwardXZ, toTileNormalized);

                float tileExtentRad = MathF.Atan(TileSize / MathF.Max(distance, TileSize));
                float cullHalfAngle = halfHorizRad + baseMarginRad + tileExtentRad;

                if (dot > MathF.Cos(cullHalfAngle) || distance < 10)
                    _renderedTiles.Add((x, y));
            }
        }
    }

    private void RenderLegacyTiles(Camera3D camera, int viewportWidth, int viewportHeight)
    {
        float aspect = viewportHeight > 0 ? viewportWidth / (float)viewportHeight : 1f;
        float halfHorizRad = ComputeHorizHalfFovRad(camera, aspect);
        float baseMarginRad = FrustumEdgeMarginDegrees * (MathF.PI / 180f);

        Vector3 cameraForward = Vector3.Normalize(camera.Target - camera.Position);
        Vector3 cameraForwardXZ = new Vector3(cameraForward.X, 0f, cameraForward.Z);
        if (cameraForwardXZ.LengthSquared() > 0.0001f)
            cameraForwardXZ = Vector3.Normalize(cameraForwardXZ);
        else
            cameraForwardXZ = new Vector3(0f, 0f, 1f);

        Vector3 cameraPosXZ = new Vector3(camera.Position.X, 0, camera.Position.Z);
        float drawDistanceWorld = _drawDistance * TileSize;

        int cameraTileX = (int)(camera.Position.X / TileSize + 0.5f);
        int cameraTileY = (int)(camera.Position.Z / TileSize + 0.5f);
        int minX = Math.Max(0, cameraTileX - (int)_drawDistance);
        int maxX = Math.Min(_level.Width - 1, cameraTileX + (int)_drawDistance);
        int minY = Math.Max(0, cameraTileY - (int)_drawDistance);
        int maxY = Math.Min(_level.Height - 1, cameraTileY + (int)_drawDistance);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector3 tilePos = new Vector3(x * TileSize, 0, y * TileSize);
                Vector3 toTile = tilePos - cameraPosXZ;
                float distance = toTile.Length();

                if (distance > drawDistanceWorld) continue;

                Vector3 toTileNormalized = Vector3.Normalize(toTile);
                float dot = Vector3.Dot(cameraForwardXZ, toTileNormalized);

                float tileExtentRad = MathF.Atan(TileSize / MathF.Max(distance, TileSize));
                float cullHalfAngle = halfHorizRad + baseMarginRad + tileExtentRad;

                if (dot > MathF.Cos(cullHalfAngle) || distance < 10)
                {
                    RenderTile(x, y, tilePos, camera.Position);
                    _renderedTiles.Add((x, y));
                }
            }
        }
    }

    /// <summary>
    /// Horizontal half-FOV in radians. Matches Raylib perspective: vertical FOV from the camera, horizontal from aspect.
    /// </summary>
    private static float ComputeHorizHalfFovRad(Camera3D camera, float viewportAspect)
    {
        float halfVertRad = camera.FovY * (MathF.PI / 180f) * 0.5f;
        return MathF.Atan(MathF.Tan(halfVertRad) * viewportAspect);
    }

    private void RenderTile(int x, int y, Vector3 worldPos, Vector3 playerPosition)
    {
        var wallTile = _level.GetWallTile(x, y);
        if (wallTile != 0 && wallTile <= _textures.Count)
        {
            PrimitiveRenderer.DrawCubeTexture(
                _textures[(int)wallTile - 1],
                new Vector3(worldPos.X, 2, worldPos.Z),
                4.0f, 4.0f, 4.0f,
                Color.White,
                playerPosition
            );
        }

        RenderFloorAndCeiling(x, y, worldPos);
    }

    public void Dispose() => _staticMeshes.Dispose();
}
