using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Engine.Rendering;

public class RenderSystem
{
    private readonly LevelData _level;
    private readonly List<Texture2D> _textures;
    private readonly float _drawDistance;
    private const float TileSize = 4.0f;
    /// <summary>Extra half-angle beyond the view frustum (projection / pitch slack).</summary>
    private const float FrustumEdgeMarginDegrees = 5f;

    // Track rendered tiles for minimap
    private HashSet<(int x, int y)> _renderedTiles = new HashSet<(int x, int y)>();

    public HashSet<(int x, int y)> RenderedTiles => _renderedTiles;

    public RenderSystem(LevelData level, List<Texture2D> textures, float drawDistance = 15.0f)
    {
        _level = level;
        _textures = textures;
        _drawDistance = drawDistance;
    }

    public void Render(Camera3D camera, int viewportWidth, int viewportHeight)
    {
        // Reset number of quads that is being drawed
        LevelData.DrawedQuads = 0;

        // Clear rendered tiles tracking
        _renderedTiles.Clear();

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

                // Widen cull cone by tile width at this distance so full quads at the screen edge are included.
                float tileExtentRad = MathF.Atan(TileSize / MathF.Max(distance, TileSize));
                float cullHalfAngle = halfHorizRad + baseMarginRad + tileExtentRad;

                if (dot > MathF.Cos(cullHalfAngle) || distance < 10)
                {
                    RenderTile(x, y, tilePos, camera.Position);
                    // Track that this tile was rendered
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
        // Draw walls
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

        // Draw floors
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

        // Draw ceilings
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
}
