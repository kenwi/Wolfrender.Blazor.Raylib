using System.Numerics;
using Game.Features.Doors;
using Game.Features.Enemies;
using Game.Features.Pickups;
using Game.Features.Players;
using Raylib_cs;
using static Raylib_cs.Raylib;
using Color = Raylib_cs.Color;

namespace Game.Editor;

/// <summary>
/// Renders the 2D map view in the level editor: grid, tile layers, doors, enemies, player, FOV, patrol paths.
/// </summary>
public class EditorMapRenderer
{
    private readonly MapData _mapData;

    public EditorMapRenderer(MapData mapData)
    {
        _mapData = mapData;
    }

    public void DrawMapGrid(EditorCamera camera)
    {
        float tileSize = camera.TileSize;
        int screenW = GetScreenWidth();
        int screenH = GetScreenHeight();

        int startX = Math.Max(0, (int)((-camera.Offset.X) / tileSize));
        int startY = Math.Max(0, (int)((-camera.Offset.Y) / tileSize));
        int endX = Math.Min(_mapData.Width, (int)((screenW - camera.Offset.X) / tileSize) + 1);
        int endY = Math.Min(_mapData.Height, (int)((screenH - camera.Offset.Y) / tileSize) + 1);

        var gridColor = new Color(60, 60, 60, 255);

        for (int x = startX; x <= endX; x++)
        {
            int screenX = (int)(x * tileSize + camera.Offset.X);
            DrawLine(screenX, Math.Max(0, (int)(startY * tileSize + camera.Offset.Y)),
                     screenX, Math.Min(screenH, (int)(endY * tileSize + camera.Offset.Y)), gridColor);
        }

        for (int y = startY; y <= endY; y++)
        {
            int screenY = (int)(y * tileSize + camera.Offset.Y);
            DrawLine(Math.Max(0, (int)(startX * tileSize + camera.Offset.X)), screenY,
                     Math.Min(screenW, (int)(endX * tileSize + camera.Offset.X)), screenY, gridColor);
        }
    }

    public void RenderLayer(EditorLayer layer, EditorCamera camera)
    {
        float tileSize = camera.TileSize;
        int screenW = GetScreenWidth();
        int screenH = GetScreenHeight();

        int startX = Math.Max(0, (int)((-camera.Offset.X) / tileSize));
        int startY = Math.Max(0, (int)((-camera.Offset.Y) / tileSize));
        int endX = Math.Min(_mapData.Width - 1, (int)((screenW - camera.Offset.X) / tileSize) + 1);
        int endY = Math.Min(_mapData.Height - 1, (int)((screenH - camera.Offset.Y) / tileSize) + 1);

        var ids = layer.Tiles;
        if (ids == null || ids.Length == 0) return;

        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                int index = _mapData.Width * y + x;
                uint tileId = ids[index];
                if (tileId == 0) continue;

                float drawX = x * tileSize + camera.Offset.X;
                float drawY = y * tileSize + camera.Offset.Y;

                bool isDoorTile = DoorTileEncoding.TryParse(tileId, out var doorInfo);
                bool isVerticalDoor = isDoorTile && doorInfo.Rotation == DoorRotation.VERTICAL;
                int textureIndex = isDoorTile
                    ? doorInfo.TextureIndex
                    : (int)tileId - 1;

                if (textureIndex >= 0 && textureIndex < _mapData.TileTextures.Count)
                {
                    var texture = _mapData.TileTextures[textureIndex];
                    float rotation = isVerticalDoor ? 90f : 0f;
                    var origin = isVerticalDoor
                        ? new Vector2(tileSize / 2f, tileSize / 2f)
                        : Vector2.Zero;
                    var destX = isVerticalDoor ? drawX + tileSize / 2f : drawX;
                    var destY = isVerticalDoor ? drawY + tileSize / 2f : drawY;

                    DrawTexturePro(
                        texture,
                        new Rectangle(0, 0, texture.Width, texture.Height),
                        new Rectangle(destX, destY, tileSize, tileSize),
                        origin,
                        rotation,
                        Color.White
                    );

                    if (isDoorTile && doorInfo.LockKind != DoorLockKind.None)
                    {
                        var lockColor = doorInfo.LockKind == DoorLockKind.Gold
                            ? new Color(255, 210, 40, 255)
                            : new Color(200, 220, 255, 255);
                        string label = doorInfo.LockKind == DoorLockKind.Gold ? "G" : "S";
                        DrawCircle((int)(drawX + tileSize * 0.75f), (int)(drawY + tileSize * 0.25f), tileSize * 0.2f, lockColor);
                        DrawText(label, (int)(drawX + tileSize * 0.65f), (int)(drawY + tileSize * 0.12f), (int)(tileSize * 0.35f), Color.Black);
                    }
                }
                else
                {
                    DrawRectangle((int)drawX, (int)drawY, (int)tileSize, (int)tileSize, Color.Magenta);
                }
            }
        }
    }

    public void RenderLiveDoors(DoorSystem doorSystem, EditorCamera camera)
    {
        if (doorSystem.Doors == null) return;

        float tileSize = camera.TileSize;
        foreach (var door in doorSystem.Doors)
        {
            if (door.TextureIndex < 0 || door.TextureIndex >= _mapData.TileTextures.Count)
                continue;

            var texture = _mapData.TileTextures[door.TextureIndex];
            float drawX = door.Position.X * tileSize + camera.Offset.X;
            float drawY = door.Position.Y * tileSize + camera.Offset.Y;

            bool isVertical = door.DoorRotation == DoorRotation.VERTICAL;
            float rotation = isVertical ? 90f : 0f;
            var origin = isVertical
                ? new Vector2(tileSize / 2f, tileSize / 2f)
                : Vector2.Zero;
            var destX = isVertical ? drawX + tileSize / 2f : drawX;
            var destY = isVertical ? drawY + tileSize / 2f : drawY;

            DrawTexturePro(
                texture,
                new Rectangle(0, 0, texture.Width, texture.Height),
                new Rectangle(destX, destY, tileSize, tileSize),
                origin,
                rotation,
                Color.White
            );
        }
    }

    public void RenderPlayerIndicator(
        Player player, EditorCamera camera, float spawnRotation,
        bool hoveredPlayer, bool isPlayerSelected, bool isDraggingPlayer)
    {
        float tileSize = camera.TileSize;
        float quadSize = LevelData.QuadSize;

        var screen = WorldAnchorToTileCenterScreen(
            player.Position.X, player.Position.Z, quadSize, tileSize, camera.Offset);
        float screenX = screen.X;
        float screenY = screen.Y;

        float radius = tileSize * 0.35f;

        DrawCircle((int)screenX, (int)screenY, radius, new Color(30, 120, 255, 200));

        if (hoveredPlayer)
        {
            DrawCircleLines((int)screenX, (int)screenY, radius + 2f, Color.Yellow);
            DrawCircleLines((int)screenX, (int)screenY, radius + 3f, Color.Yellow);
        }

        if (isPlayerSelected)
        {
            DrawCircleLines((int)screenX, (int)screenY, radius + 1f, Color.White);
            DrawCircleLines((int)screenX, (int)screenY, radius + 4f, Color.White);
        }
        else if (isDraggingPlayer)
        {
            DrawCircleLines((int)screenX, (int)screenY, radius + 1f, Color.White);
            DrawCircleLines((int)screenX, (int)screenY, radius + 4f, Color.White);
        }
        else
        {
            DrawCircleLines((int)screenX, (int)screenY, radius, new Color(80, 170, 255, 255));
            DrawCircleLines((int)screenX, (int)screenY, radius + 1f, new Color(80, 170, 255, 255));
        }

        float angle = spawnRotation;
        float dirLen = radius * 0.9f;
        float endX = screenX + MathF.Cos(angle) * dirLen;
        float endY = screenY + MathF.Sin(angle) * dirLen;
        DrawLineEx(new Vector2(screenX, screenY), new Vector2(endX, endY), 2f, Color.White);

        const string label = "Player";
        int labelW = MeasureText(label, 14);
        DrawText(label, (int)(screenX - labelW / 2f), (int)(screenY - radius - 16), 14, new Color(80, 170, 255, 255));
    }

    public void RenderEnemyLayer(
        EditorCamera camera, EnemySystem enemySystem, bool isMouseOverUI,
        bool isSimulating, bool drawEnemyLineOfSight, bool showPatrolPaths,
        ref int hoveredEnemyIndex, int selectedEnemyIndex,
        bool isEditingPatrolPath, int patrolEditEnemyIndex, List<PatrolWaypoint> patrolPathInProgress)
    {
        float tileSize = camera.TileSize;
        var mouseScreen = GetMousePosition();
        hoveredEnemyIndex = -1;

        float radius = tileSize * 0.35f;

        for (int i = 0; i < _mapData.Enemies.Count; i++)
        {
            var enemy = _mapData.Enemies[i];

            var center = TileToCenterScreen(enemy.TileX, enemy.TileY, tileSize, camera.Offset);
            float centerX = center.X;
            float centerY = center.Y;

            if (!isMouseOverUI)
            {
                float dx = mouseScreen.X - centerX;
                float dy = mouseScreen.Y - centerY;
                if (dx * dx + dy * dy <= radius * radius)
                {
                    hoveredEnemyIndex = i;
                }
            }

            var fillColor = enemy.StartsAsCorpse
                ? new Color(90, 90, 90, 200)
                : new Color(200, 40, 40, 200);
            DrawCircle((int)centerX, (int)centerY, radius, fillColor);

            if (i == hoveredEnemyIndex)
            {
                DrawCircleLines((int)centerX, (int)centerY, radius + 2f, Color.Yellow);
                DrawCircleLines((int)centerX, (int)centerY, radius + 3f, Color.Yellow);
            }

            if (i == selectedEnemyIndex)
            {
                DrawCircleLines((int)centerX, (int)centerY, radius + 1f, Color.White);
                DrawCircleLines((int)centerX, (int)centerY, radius + 4f, Color.White);
            }

            float dirLen = radius * 0.8f;
            float angle = enemy.Rotation;
            float endX = centerX + MathF.Cos(angle) * dirLen;
            float endY = centerY + MathF.Sin(angle) * dirLen;
            DrawLineEx(new Vector2(centerX, centerY), new Vector2(endX, endY), 2f, Color.White);

            if (showPatrolPaths && enemy.ShowPatrolPath && enemy.PatrolPath.Count > 0)
            {
                DrawPatrolPath(enemy, enemy.PatrolPath, camera, new Color(0, 200, 255, 200));
            }

            if (isEditingPatrolPath && patrolEditEnemyIndex == i && patrolPathInProgress.Count > 0)
            {
                DrawPatrolPath(enemy, patrolPathInProgress, camera, new Color(255, 200, 0, 220));

                var lastWp = patrolPathInProgress[^1];
                var lastWpScreen = TileToCenterScreen(lastWp.TileX, lastWp.TileY, tileSize, camera.Offset);
                float lastWpX = lastWpScreen.X;
                float lastWpY = lastWpScreen.Y;
                DrawLineEx(new Vector2(lastWpX, lastWpY), mouseScreen, 1f, new Color(255, 200, 0, 120));
            }
        }

        // When simulating, draw live enemy positions (optional FOV overlay)
        if (isSimulating && enemySystem.Enemies != null)
        {
            RenderLiveEnemies(enemySystem, camera, drawEnemyLineOfSight);
        }
    }

    public void RenderObjectLayer(EditorCamera camera)
    {
        float tileSize = camera.TileSize;
        int screenW = GetScreenWidth();
        int screenH = GetScreenHeight();

        int startX = Math.Max(0, (int)((-camera.Offset.X) / tileSize));
        int startY = Math.Max(0, (int)((-camera.Offset.Y) / tileSize));
        int endX = Math.Min(_mapData.Width - 1, (int)((screenW - camera.Offset.X) / tileSize) + 1);
        int endY = Math.Min(_mapData.Height - 1, (int)((screenH - camera.Offset.Y) / tileSize) + 1);

        var objectsTex = _mapData.GameTextures.Count > PickupSprites.ObjectsTextureIndex
            ? _mapData.GameTextures[PickupSprites.ObjectsTextureIndex]
            : default;
        if (objectsTex.Id == 0)
            return;

        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                uint objectId = _mapData.Objects[_mapData.Width * y + x];
                if (!ObjectSprites.IsValidObjectId(objectId))
                    continue;

                float drawX = x * tileSize + camera.Offset.X;
                float drawY = y * tileSize + camera.Offset.Y;
                float visualY = drawY - tileSize * 0.5f;
                var dest = new Rectangle(drawX, visualY, tileSize, tileSize);
                PrimitiveRenderer.DrawScreenSprite(
                    objectsTex,
                    ObjectSprites.GetFrameRectForObjectId(objectId),
                    dest,
                    Color.White);
            }
        }
    }

    public void RenderPickupLayer(
        EditorCamera camera,
        bool isMouseOverUI,
        ref int hoveredPickupIndex,
        int selectedPickupIndex)
    {
        float tileSize = camera.TileSize;
        var mouseScreen = GetMousePosition();
        hoveredPickupIndex = -1;

        var objectsTex = _mapData.GameTextures.Count > PickupSprites.ObjectsTextureIndex
            ? _mapData.GameTextures[PickupSprites.ObjectsTextureIndex]
            : default;
        bool hasSpriteSheet = objectsTex.Id != 0;

        float radius = tileSize * 0.35f;

        for (int i = 0; i < _mapData.Pickups.Count; i++)
        {
            var pickup = _mapData.Pickups[i];
            float drawX = pickup.TileX * tileSize + camera.Offset.X;
            float drawY = pickup.TileY * tileSize + camera.Offset.Y;

            var center = TileToCenterScreen(pickup.TileX, pickup.TileY, tileSize, camera.Offset);
            float centerX = center.X;
            float centerY = center.Y;

            if (!isMouseOverUI)
            {
                float dx = mouseScreen.X - centerX;
                float dy = mouseScreen.Y - centerY;
                if (dx * dx + dy * dy <= radius * radius)
                    hoveredPickupIndex = i;
            }

            if (hasSpriteSheet)
            {
                // Visual-only offset: center the sprite on the tile (map data unchanged).
                float visualY = drawY - tileSize * 0.5f;
                var dest = new Rectangle(drawX, visualY, tileSize, tileSize);
                PrimitiveRenderer.DrawScreenSprite(
                    objectsTex,
                    PickupSprites.GetFrameRect(pickup.Type),
                    dest,
                    Color.White);
            }
            else
            {
                var color = PickupVisuals.GetColor(pickup.Type);
                DrawCircle((int)centerX, (int)centerY, radius, color);
            }

            if (i == hoveredPickupIndex)
            {
                DrawCircleLines((int)centerX, (int)centerY, radius + 2f, Color.Yellow);
                DrawCircleLines((int)centerX, (int)centerY, radius + 3f, Color.Yellow);
            }

            if (i == selectedPickupIndex)
            {
                DrawCircleLines((int)centerX, (int)centerY, radius + 1f, Color.White);
                DrawCircleLines((int)centerX, (int)centerY, radius + 4f, Color.White);
            }
        }
    }

    private void RenderLiveEnemies(EnemySystem enemySystem, EditorCamera camera, bool drawLineOfSight)
    {
        float tileSize = camera.TileSize;
        float liveRadius = tileSize * 0.3f;
        float quadSize = LevelData.QuadSize;

        foreach (var liveEnemy in enemySystem.Enemies)
        {
            var liveCenter = WorldAnchorToTileCenterScreen(
                liveEnemy.Position.X, liveEnemy.Position.Z, quadSize, tileSize, camera.Offset);
            float liveCX = liveCenter.X;
            float liveCY = liveCenter.Y;

            // Draw FOV polygon
            if (drawLineOfSight && liveEnemy.FovPolygon.Count >= 3)
            {
                var origin = liveEnemy.FovPolygon[0];
                float originScreenX = origin.X * tileSize + camera.Offset.X;
                float originScreenY = origin.Y * tileSize + camera.Offset.Y;

                var fovFillColor = liveEnemy.CanSeePlayer
                    ? new Color(255, 40, 40, 50)
                    : new Color(255, 200, 0, 50);

                var fovEdgeColor = liveEnemy.CanSeePlayer
                    ? new Color(255, 80, 80, 180)
                    : new Color(255, 200, 0, 140);

                for (int r = 1; r < liveEnemy.FovPolygon.Count - 1; r++)
                {
                    var p1 = liveEnemy.FovPolygon[r];
                    var p2 = liveEnemy.FovPolygon[r + 1];

                    float p1x = p1.X * tileSize + camera.Offset.X;
                    float p1y = p1.Y * tileSize + camera.Offset.Y;
                    float p2x = p2.X * tileSize + camera.Offset.X;
                    float p2y = p2.Y * tileSize + camera.Offset.Y;

                    DrawTriangle(
                        new Vector2(originScreenX, originScreenY),
                        new Vector2(p1x, p1y),
                        new Vector2(p2x, p2y),
                        fovFillColor);

                    DrawTriangle(
                        new Vector2(originScreenX, originScreenY),
                        new Vector2(p2x, p2y),
                        new Vector2(p1x, p1y),
                        fovFillColor);
                }

                for (int r = 1; r < liveEnemy.FovPolygon.Count; r++)
                {
                    var p = liveEnemy.FovPolygon[r];
                    float px = p.X * tileSize + camera.Offset.X;
                    float py = p.Y * tileSize + camera.Offset.Y;

                    if (r == 1 || r == liveEnemy.FovPolygon.Count - 1)
                    {
                        DrawLineEx(
                            new Vector2(originScreenX, originScreenY),
                            new Vector2(px, py),
                            1f, fovEdgeColor);
                    }
                }

                for (int r = 1; r < liveEnemy.FovPolygon.Count - 1; r++)
                {
                    var p1 = liveEnemy.FovPolygon[r];
                    var p2 = liveEnemy.FovPolygon[r + 1];
                    float p1x = p1.X * tileSize + camera.Offset.X;
                    float p1y = p1.Y * tileSize + camera.Offset.Y;
                    float p2x = p2.X * tileSize + camera.Offset.X;
                    float p2y = p2.Y * tileSize + camera.Offset.Y;

                    DrawLineEx(
                        new Vector2(p1x, p1y),
                        new Vector2(p2x, p2y),
                        1f, fovEdgeColor);
                }
            }

            DrawCircle((int)liveCX, (int)liveCY, liveRadius, new Color(40, 200, 40, 180));
            DrawCircleLines((int)liveCX, (int)liveCY, liveRadius, new Color(40, 255, 40, 255));

            float liveDirLen = liveRadius * 0.8f;
            float liveAngle = liveEnemy.Rotation;
            float liveEndX = liveCX + MathF.Cos(liveAngle) * liveDirLen;
            float liveEndY = liveCY + MathF.Sin(liveAngle) * liveDirLen;
            DrawLineEx(new Vector2(liveCX, liveCY), new Vector2(liveEndX, liveEndY), 2f, Color.White);

            string stateText = liveEnemy.CanSeePlayer ? "SPOTTED!" : liveEnemy.EnemyState.ToString();
            int stateW = MeasureText(stateText, 14);
            Color stateColor;
            if (liveEnemy.CanSeePlayer)
                stateColor = new Color(255, 0, 0, 255);
            else if (liveEnemy.EnemyState == EnemyState.COLLIDING)
                stateColor = new Color(255, 40, 40, 255);
            else if (liveEnemy.EnemyState == EnemyState.CORPSE)
                stateColor = new Color(120, 120, 120, 255);
            else if (liveEnemy.EnemyState == EnemyState.HIT)
                stateColor = new Color(255, 160, 40, 255);
            else
                stateColor = new Color(40, 255, 40, 255);
            DrawText(stateText, (int)(liveCX - stateW / 2f), (int)(liveCY - liveRadius - 16), 14, stateColor);
        }
    }

    public void DrawPatrolPath(EnemyPlacement enemy, List<PatrolWaypoint> path, EditorCamera camera, Color color)
    {
        float tileSize = camera.TileSize;
        var prev = TileToCenterScreen(enemy.TileX, enemy.TileY, tileSize, camera.Offset);
        float prevX = prev.X;
        float prevY = prev.Y;

        for (int w = 0; w < path.Count; w++)
        {
            var wpScreen = TileToCenterScreen(path[w].TileX, path[w].TileY, tileSize, camera.Offset);
            float wpX = wpScreen.X;
            float wpY = wpScreen.Y;

            DrawLineEx(new Vector2(prevX, prevY), new Vector2(wpX, wpY), 2f, color);
            DrawCircle((int)wpX, (int)wpY, tileSize * 0.12f, color);

            prevX = wpX;
            prevY = wpY;
        }
    }

    /// <summary>
    /// Draw each live enemy's current A* chase path (orange polyline from the enemy's
    /// position through its remaining waypoints). Used by the Pathfinding Visualizer's
    /// "Draw paths for enemies" toggle while simulating.
    /// </summary>
    public void DrawEnemyChasePaths(EnemySystem enemySystem, EditorCamera camera)
    {
        float tileSize = camera.TileSize;
        float quadSize = LevelData.QuadSize;
        var pathColor = new Color(255, 140, 0, 220);
        var fallbackColor = new Color(255, 80, 0, 150);

        foreach (var enemy in enemySystem.Enemies)
        {
            var prevScreen = WorldAnchorToTileCenterScreen(
                enemy.Position.X, enemy.Position.Z, quadSize, tileSize, camera.Offset);
            float prevX = prevScreen.X;
            float prevY = prevScreen.Y;

            bool hasChasePath =
                enemy.ChasePath.Count > 0 && enemy.ChasePathIndex < enemy.ChasePath.Count;

            if (hasChasePath)
            {
                for (int i = enemy.ChasePathIndex; i < enemy.ChasePath.Count; i++)
                {
                    var wp = enemy.ChasePath[i];
                    var wpScreen = WorldAnchorToTileCenterScreen(wp.X, wp.Z, quadSize, tileSize, camera.Offset);
                    float wpX = wpScreen.X;
                    float wpY = wpScreen.Y;
                    DrawLineEx(new Vector2(prevX, prevY), new Vector2(wpX, wpY), 2.5f, pathColor);
                    DrawCircle((int)wpX, (int)wpY, tileSize * 0.1f, pathColor);
                    prevX = wpX;
                    prevY = wpY;
                }
            }
            else if (enemy.LastSeenPlayerPosition.HasValue)
            {
                // No (or exhausted) A* path but the enemy is still steering toward the
                // player's last known position — draw a thin fallback line so this case
                // is visible in the debug overlay.
                var ls = enemy.LastSeenPlayerPosition.Value;
                var lsScreen = WorldAnchorToTileCenterScreen(ls.X, ls.Z, quadSize, tileSize, camera.Offset);
                float lx = lsScreen.X;
                float ly = lsScreen.Y;
                DrawLineEx(new Vector2(prevX, prevY), new Vector2(lx, ly), 1.5f, fallbackColor);
                DrawCircleLines((int)lx, (int)ly, tileSize * 0.18f, fallbackColor);
            }
        }
    }

    /// <summary>
    /// Draw the pathfinding visualizer overlay: start/end tile highlights and the
    /// computed A* path as a polyline through tile centers.
    /// </summary>
    public void DrawPathPreview(
        Vector2? start, Vector2? end, List<Vector2>? path, EditorCamera camera)
    {
        float tileSize = camera.TileSize;

        if (start.HasValue)
            FillTile(start.Value, tileSize, camera.Offset,
                fill: new Color(60, 220, 60, 110), border: Color.Green);

        if (end.HasValue)
            FillTile(end.Value, tileSize, camera.Offset,
                fill: new Color(220, 60, 60, 110), border: Color.Red);

        if (path == null || path.Count < 2) return;

        var pathColor = new Color(0, 200, 255, 230);
        for (int i = 0; i < path.Count - 1; i++)
        {
            var a = TileCenter(path[i], tileSize, camera.Offset);
            var b = TileCenter(path[i + 1], tileSize, camera.Offset);
            DrawLineEx(a, b, 3f, pathColor);
        }

        foreach (var p in path)
        {
            var c = TileCenter(p, tileSize, camera.Offset);
            DrawCircle((int)c.X, (int)c.Y, tileSize * 0.1f, pathColor);
        }
    }

    private static void FillTile(Vector2 tile, float tileSize, Vector2 offset, Color fill, Color border)
    {
        float x = tile.X * tileSize + offset.X;
        float y = tile.Y * tileSize + offset.Y;
        DrawRectangle((int)x, (int)y, (int)tileSize, (int)tileSize, fill);
        DrawRectangleLinesEx(new Rectangle(x, y, tileSize, tileSize), 2f, border);
    }

    private static Vector2 TileCenter(Vector2 tile, float tileSize, Vector2 offset) =>
        new((tile.X + 0.5f) * tileSize + offset.X, (tile.Y + 0.5f) * tileSize + offset.Y);

    /// <summary>2D editor: center of the tile square for integer tile indices.</summary>
    private static Vector2 TileToCenterScreen(int tileX, int tileY, float tileSize, Vector2 offset) =>
        new((tileX + 0.5f) * tileSize + offset.X, (tileY + 0.5f) * tileSize + offset.Y);

    /// <summary>
    /// 2D editor: game entities use <see cref="LevelData.GetTileAnchorWorld"/>; show them at the
    /// visual center of that tile (anchor + half a tile in screen space).
    /// </summary>
    private static Vector2 WorldAnchorToTileCenterScreen(
        float worldX, float worldZ, float quadSize, float tileSize, Vector2 offset)
    {
        float tileX = worldX / quadSize;
        float tileY = worldZ / quadSize;
        return new((tileX + 0.5f) * tileSize + offset.X, (tileY + 0.5f) * tileSize + offset.Y);
    }

    /// <summary>
    /// Draw a yellow highlight rectangle around the hovered tile.
    /// </summary>
    public void DrawTileHighlight(int tileX, int tileY, EditorCamera camera)
    {
        float tileSize = camera.TileSize;
        float highlightX = tileX * tileSize + camera.Offset.X;
        float highlightY = tileY * tileSize + camera.Offset.Y;
        DrawRectangleLinesEx(
            new Rectangle(highlightX, highlightY, tileSize, tileSize),
            2f, Color.Yellow);
    }
}
