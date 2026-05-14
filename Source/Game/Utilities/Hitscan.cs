using System.Collections.Generic;
using System.Numerics;
using Game.Entities;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Utilities;

/// <summary>
/// Horizontal hitscan against walls/doors (DDA) and enemy cylinders in XZ.
/// </summary>
public static class Hitscan
{
    public const float DefaultMaxRangeTiles = 48f;
    public const float DefaultEnemyHitRadiusWorld = 2f;

    private static readonly Dictionary<uint, Image> _textureCpuImageCache = new();

    public static bool TryHitEnemy(
        MapData mapData,
        List<Door> doors,
        Vector3 rayOriginWorld,
        Vector3 rayDirectionWorld,
        IReadOnlyList<Enemy> enemies,
        float enemyHitRadiusWorld,
        float maxRangeTiles,
        out Enemy? hitEnemy)
    {
        hitEnemy = null;

        var dirHoriz = new Vector3(rayDirectionWorld.X, 0f, rayDirectionWorld.Z);
        if (dirHoriz.LengthSquared() < 1e-8f)
            return false;

        dirHoriz = Vector3.Normalize(dirHoriz);
        float quad = LevelData.QuadSize;

        var startTile = new Vector2(rayOriginWorld.X / quad + 0.5f, rayOriginWorld.Z / quad + 0.5f);
        var dirTile = new Vector2(dirHoriz.X, dirHoriz.Z);
        if (dirTile.LengthSquared() < 1e-8f)
            return false;
        dirTile = Vector2.Normalize(dirTile);

        var wallHit = LineOfSight.CastRay(mapData, doors, startTile, dirTile, maxRangeTiles);
        float wallDistanceWorld = Vector2.Distance(startTile, wallHit) * quad;

        Vector2 originXz = new(rayOriginWorld.X, rayOriginWorld.Z);
        Vector2 dirXz = new(dirHoriz.X, dirHoriz.Z);

        float bestT = float.MaxValue;
        foreach (var enemy in enemies)
        {
            if (!enemy.IsCombatActive)
                continue;

            Vector2 center = new(enemy.Position.X, enemy.Position.Z);
            Vector2 toCircle = center - originXz;
            float t = Vector2.Dot(toCircle, dirXz);
            if (t < 0f || t > wallDistanceWorld + 0.001f)
                continue;

            Vector2 closest = originXz + dirXz * t;
            float r = enemyHitRadiusWorld;
            if (Vector2.DistanceSquared(center, closest) > r * r)
                continue;

            if (t < bestT)
            {
                bestT = t;
                hitEnemy = enemy;
            }
        }

        return hitEnemy != null;
    }

    /// <summary>
    /// Center-screen ray from <see cref="GetScreenToWorldRayEx"/> vs each enemy billboard quad
    /// (<see cref="GetRayCollisionQuad"/>). Uses horizontal DDA only to reject hits occluded by walls/doors
    /// (same tile-space idea as <see cref="TryHitEnemy"/>, not full 3D ray-marched occlusion).
    /// </summary>
    public static bool TryHitEnemyScreenRay(
        MapData mapData,
        List<Door> doors,
        Camera3D camera,
        int screenWidth,
        int screenHeight,
        IReadOnlyList<Enemy> enemies,
        Texture2D enemySpriteSheet,
        float spriteWidth,
        float spriteHeight,
        float spriteYAxisRotationRadians,
        float maxRangeTiles,
        out Enemy? hitEnemy)
    {
        hitEnemy = null;
        if (screenWidth <= 0 || screenHeight <= 0)
            return false;

        var ray = GetScreenToWorldRayEx(
            new Vector2(screenWidth / 2f, screenHeight / 2f),
            camera,
            screenWidth,
            screenHeight);

        Vector3 cameraPos = camera.Position;
        float bestDist = float.MaxValue;
        Enemy? best = null;

        foreach (var enemy in enemies)
        {
            if (!enemy.IsCombatActive)
                continue;

            SpriteBillboardGeometry.ComputeBillboardQuad(
                enemy.Position,
                cameraPos,
                spriteWidth,
                spriteHeight,
                spriteYAxisRotationRadians,
                out Vector3 topLeft,
                out Vector3 topRight,
                out Vector3 bottomRight,
                out Vector3 bottomLeft);

            RayCollision col = GetRayCollisionQuad(ray, topLeft, topRight, bottomRight, bottomLeft);
            if (!col.Hit)
                continue;

            if (IsWallOccludingHorizontalShot(mapData, doors, cameraPos, col.Point, maxRangeTiles))
                continue;

            if (!IsBillboardHitOnOpaqueSpriteTexel(
                    enemySpriteSheet,
                    enemy.FrameRect,
                    col.Point,
                    topLeft,
                    topRight,
                    bottomLeft))
                continue;

            if (col.Distance < bestDist)
            {
                bestDist = col.Distance;
                best = enemy;
            }
        }

        hitEnemy = best;
        return best != null;
    }

    /// <summary>
    /// Same as the overload that takes <c>spriteYAxisRotationRadians</c>, using <c>0</c> radians
    /// (matches current <see cref="PrimitiveRenderer.DrawSpriteTexture"/> call sites).
    /// </summary>
    public static bool TryHitEnemyScreenRay(
        MapData mapData,
        List<Door> doors,
        Camera3D camera,
        int screenWidth,
        int screenHeight,
        IReadOnlyList<Enemy> enemies,
        Texture2D enemySpriteSheet,
        float spriteWidth,
        float spriteHeight,
        float maxRangeTiles,
        out Enemy? hitEnemy)
    {
        return TryHitEnemyScreenRay(
            mapData,
            doors,
            camera,
            screenWidth,
            screenHeight,
            enemies,
            enemySpriteSheet,
            spriteWidth,
            spriteHeight,
            0f,
            maxRangeTiles,
            out hitEnemy);
    }

    /// <summary>
    /// True if a wall or closed door blocks the horizontal segment from the camera to the hit point in XZ.
    /// </summary>
    private static bool IsWallOccludingHorizontalShot(
        MapData mapData,
        List<Door> doors,
        Vector3 rayOrigin,
        Vector3 hitWorld,
        float maxRangeTiles)
    {
        float quad = LevelData.QuadSize;
        var start = new Vector2(rayOrigin.X / quad + 0.5f, rayOrigin.Z / quad + 0.5f);
        var delta = new Vector2(hitWorld.X - rayOrigin.X, hitWorld.Z - rayOrigin.Z);
        float distToHit = delta.Length();
        if (distToHit < 1e-3f)
            return false;

        var dir = delta / distToHit;
        float maxTileDist = distToHit / quad + 1f;
        float rayMaxTiles = MathF.Min(maxRangeTiles, maxTileDist);
        var wallHit = LineOfSight.CastRay(mapData, doors, start, dir, rayMaxTiles);
        float wallDistWorld = Vector2.Distance(start, wallHit) * quad;
        return wallDistWorld < distToHit - 0.2f;
    }

    /// <summary>
    /// CPU sample of the sprite sheet at the ray hit — matches <see cref="PrimitiveRenderer.DrawSpriteTexture"/> UV layout.
    /// Returns false when the texel matches <see cref="PrimitiveRenderer.SpriteTransparencyKey"/> (treated as empty / miss).
    /// </summary>
    private static bool IsBillboardHitOnOpaqueSpriteTexel(
        Texture2D texture,
        Rectangle frame,
        Vector3 hitPoint,
        Vector3 topLeft,
        Vector3 topRight,
        Vector3 bottomLeft)
    {
        int tw = texture.Width;
        int th = texture.Height;
        if (tw <= 0 || th <= 0 || frame.Width <= 0 || frame.Height <= 0)
            return true;

        var e1 = topRight - topLeft;
        var e2 = bottomLeft - topLeft;
        float d1 = e1.LengthSquared();
        float d2 = e2.LengthSquared();
        if (d1 < 1e-10f || d2 < 1e-10f)
            return true;

        var rel = hitPoint - topLeft;
        float a = Vector3.Dot(rel, e1) / d1;
        float b = Vector3.Dot(rel, e2) / d2;
        a = Math.Clamp(a, 0f, 1f);
        b = Math.Clamp(b, 0f, 1f);

        float texLeft = frame.X / tw;
        float texRight = (frame.X + frame.Width) / tw;
        float texTop = frame.Y / th;
        float texBottom = (frame.Y + frame.Height) / th;

        float texU = texRight + a * (texLeft - texRight);
        float texV = texTop + b * (texBottom - texTop);

        Image image = GetCachedCpuImage(texture);
        int ix = (int)Math.Clamp(Math.Floor(texU * tw), 0, tw - 1);
        int iy = (int)Math.Clamp(Math.Floor(texV * th), 0, th - 1);
        Color sampled = GetImageColor(image, ix, iy);
        return !MatchesSpriteTransparencyKey(sampled);
    }

    private static bool MatchesSpriteTransparencyKey(Color c)
    {
        Color k = PrimitiveRenderer.SpriteTransparencyKey;
        return c.R == k.R && c.G == k.G && c.B == k.B;
    }

    private static Image GetCachedCpuImage(Texture2D texture)
    {
        uint id = texture.Id;
        if (!_textureCpuImageCache.TryGetValue(id, out Image image))
        {
            image = LoadImageFromTexture(texture);
            _textureCpuImageCache[id] = image;
        }

        return image;
    }
}
