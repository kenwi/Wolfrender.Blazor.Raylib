using System.Numerics;
using Game.Entities;
using Game.Systems;
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

                bool isVerticalDoor = tileId == (uint)DoorRotation.VERTICAL;
                bool isHorizontalDoor = tileId == (uint)DoorRotation.HORIZONTAL;
                int textureIndex = (isVerticalDoor || isHorizontalDoor)
                    ? (int)DoorRotation.HORIZONTAL - 1
                    : (int)tileId - 1;

                if (textureIndex >= 0 && textureIndex < _mapData.Textures.Count)
                {
                    var texture = _mapData.Textures[textureIndex];
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
        int doorTexIndex = (int)DoorRotation.HORIZONTAL - 1;
        if (doorTexIndex < 0 || doorTexIndex >= _mapData.Textures.Count) return;
        var texture = _mapData.Textures[doorTexIndex];

        foreach (var door in doorSystem.Doors)
        {
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

    public void RenderPlayerIndicator(Entities.Player player, EditorCamera camera)
    {
        float tileSize = camera.TileSize;
        float quadSize = Utilities.LevelData.QuadSize;
        float tilePosX = player.Position.X / quadSize;
        float tilePosZ = player.Position.Z / quadSize;

        float screenX = (tilePosX + 0.5f) * tileSize + camera.Offset.X;
        float screenY = (tilePosZ + 0.5f) * tileSize + camera.Offset.Y;

        float radius = tileSize * 0.35f;

        DrawCircle((int)screenX, (int)screenY, radius, new Color(30, 120, 255, 200));
        DrawCircleLines((int)screenX, (int)screenY, radius, new Color(80, 170, 255, 255));
        DrawCircleLines((int)screenX, (int)screenY, radius + 1f, new Color(80, 170, 255, 255));

        var cam = player.Camera;
        var lookDir = Vector3.Normalize(cam.Target - cam.Position);
        float angle = MathF.Atan2(lookDir.Z, lookDir.X);
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
        bool isSimulating, ref int hoveredEnemyIndex, int selectedEnemyIndex,
        bool isEditingPatrolPath, int patrolEditEnemyIndex, List<PatrolWaypoint> patrolPathInProgress)
    {
        float tileSize = camera.TileSize;
        var mouseScreen = GetMousePosition();
        hoveredEnemyIndex = -1;

        float radius = tileSize * 0.35f;

        for (int i = 0; i < _mapData.Enemies.Count; i++)
        {
            var enemy = _mapData.Enemies[i];

            float centerX = (enemy.TileX + 0.5f) * tileSize + camera.Offset.X;
            float centerY = (enemy.TileY + 0.5f) * tileSize + camera.Offset.Y;

            if (!isMouseOverUI)
            {
                float dx = mouseScreen.X - centerX;
                float dy = mouseScreen.Y - centerY;
                if (dx * dx + dy * dy <= radius * radius)
                {
                    hoveredEnemyIndex = i;
                }
            }

            DrawCircle((int)centerX, (int)centerY, radius, new Color(200, 40, 40, 200));

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

            if (enemy.ShowPatrolPath && enemy.PatrolPath.Count > 0)
            {
                DrawPatrolPath(enemy, enemy.PatrolPath, camera, new Color(0, 200, 255, 200));
            }

            if (isEditingPatrolPath && patrolEditEnemyIndex == i && patrolPathInProgress.Count > 0)
            {
                DrawPatrolPath(enemy, patrolPathInProgress, camera, new Color(255, 200, 0, 220));

                var lastWp = patrolPathInProgress[^1];
                float lastWpX = (lastWp.TileX + 0.5f) * tileSize + camera.Offset.X;
                float lastWpY = (lastWp.TileY + 0.5f) * tileSize + camera.Offset.Y;
                DrawLineEx(new Vector2(lastWpX, lastWpY), mouseScreen, 1f, new Color(255, 200, 0, 120));
            }
        }

        // When simulating, draw live enemy positions with FOV
        if (isSimulating && enemySystem.Enemies != null)
        {
            RenderLiveEnemies(enemySystem, camera);
        }
    }

    private void RenderLiveEnemies(EnemySystem enemySystem, EditorCamera camera)
    {
        float tileSize = camera.TileSize;
        float liveRadius = tileSize * 0.3f;
        float quadSize = Utilities.LevelData.QuadSize;

        foreach (var liveEnemy in enemySystem.Enemies)
        {
            float tilePosX = liveEnemy.Position.X / quadSize;
            float tilePosZ = liveEnemy.Position.Z / quadSize;

            float liveCX = (tilePosX + 0.5f) * tileSize + camera.Offset.X;
            float liveCY = (tilePosZ + 0.5f) * tileSize + camera.Offset.Y;

            // Draw FOV polygon
            if (liveEnemy.FovPolygon.Count >= 3)
            {
                var origin = liveEnemy.FovPolygon[0];
                float originScreenX = origin.X * tileSize + camera.Offset.X;
                float originScreenY = origin.Y * tileSize + camera.Offset.Y;

                var fovFillColor = liveEnemy.CanSeePlayer
                    ? new Color(255, 40, 40, 200)
                    : new Color(255, 200, 0, 200);

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
            else
                stateColor = new Color(40, 255, 40, 255);
            DrawText(stateText, (int)(liveCX - stateW / 2f), (int)(liveCY - liveRadius - 16), 14, stateColor);
        }
    }

    public void DrawPatrolPath(EnemyPlacement enemy, List<PatrolWaypoint> path, EditorCamera camera, Color color)
    {
        float tileSize = camera.TileSize;
        float prevX = (enemy.TileX + 0.5f) * tileSize + camera.Offset.X;
        float prevY = (enemy.TileY + 0.5f) * tileSize + camera.Offset.Y;

        for (int w = 0; w < path.Count; w++)
        {
            float wpX = (path[w].TileX + 0.5f) * tileSize + camera.Offset.X;
            float wpY = (path[w].TileY + 0.5f) * tileSize + camera.Offset.Y;

            DrawLineEx(new Vector2(prevX, prevY), new Vector2(wpX, wpY), 2f, color);
            DrawCircle((int)wpX, (int)wpY, tileSize * 0.12f, color);

            prevX = wpX;
            prevY = wpY;
        }
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
