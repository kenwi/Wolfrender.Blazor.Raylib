using System.Numerics;
using static Raylib_cs.Raylib;

namespace Game.Editor;

/// <summary>
/// 2D camera for the level editor. Handles panning, zooming, and coordinate conversion.
/// </summary>
public class EditorCamera
{
    public Vector2 Offset;
    public float Zoom = 4.5f;

    public const float BaseTileSize = 16f;
    private const float MinZoom = 0.25f;
    private const float MaxZoom = 10.0f;
    private const float ZoomSpeed = 0.1f;

    // Panning state
    private bool _isDragging;
    private Vector2 _lastMousePos;

    /// <summary>
    /// The current tile size in screen pixels (BaseTileSize * Zoom).
    /// </summary>
    public float TileSize => BaseTileSize * Zoom;

    /// <summary>
    /// Center the camera so the map is on screen.
    /// </summary>
    public void CenterOnMap(int mapWidth, int mapHeight)
    {
        float mapPixelWidth = mapWidth * BaseTileSize * Zoom;
        float mapPixelHeight = mapHeight * BaseTileSize * Zoom;
        Offset = new Vector2(
            (GetScreenWidth() - mapPixelWidth) / 2f,
            (GetScreenHeight() - mapPixelHeight) / 2f
        );
    }

    /// <summary>
    /// Handle panning (RMB drag + WASD) and zooming (scroll wheel + +/- keys).
    /// When disableKeyboardPan is true, WASD panning is skipped (e.g. during simulation).
    /// </summary>
    public void HandleInput(float deltaTime, bool ctrlHeld, bool isMouseOverUI, bool isKeyboardCapturedByUI = false, bool disableKeyboardPan = false)
    {
        // Pan with right mouse button drag
        if (!isMouseOverUI && IsMouseButtonDown(Raylib_cs.MouseButton.Right))
        {
            var mousePos = GetMousePosition();
            if (_isDragging)
            {
                var delta = mousePos - _lastMousePos;
                Offset += delta;
            }
            _isDragging = true;
            _lastMousePos = mousePos;
        }
        else
        {
            _isDragging = false;
        }

        // Pan with WASD keys (disabled during simulation — keys control the player instead)
        if (!disableKeyboardPan && !isKeyboardCapturedByUI)
        {
            float panSpeed = 500f * deltaTime;
            if (IsKeyDown(Raylib_cs.KeyboardKey.W)) Offset.Y += panSpeed;
            if (IsKeyDown(Raylib_cs.KeyboardKey.S)) Offset.Y -= panSpeed;
            if (IsKeyDown(Raylib_cs.KeyboardKey.A)) Offset.X += panSpeed;
            if (IsKeyDown(Raylib_cs.KeyboardKey.D)) Offset.X -= panSpeed;
        }

        // Zoom with scroll wheel (toward cursor) or +/- keys (toward center)
        float zoomDelta = 0f;
        Vector2 zoomAnchor = new Vector2(GetScreenWidth() / 2f, GetScreenHeight() / 2f);

        if (!isMouseOverUI)
        {
            float wheel = GetMouseWheelMove();
            if (Math.Abs(wheel) > 0.001f)
            {
                zoomDelta = wheel;
                zoomAnchor = GetMousePosition();
            }
        }

        if (!ctrlHeld && (IsKeyDown(Raylib_cs.KeyboardKey.Equal) || IsKeyDown(Raylib_cs.KeyboardKey.KpAdd)))
        {
            zoomDelta = 1f * deltaTime * 5f;
        }
        else if (!ctrlHeld && (IsKeyDown(Raylib_cs.KeyboardKey.Minus) || IsKeyDown(Raylib_cs.KeyboardKey.KpSubtract)))
        {
            zoomDelta = -1f * deltaTime * 5f;
        }

        if (Math.Abs(zoomDelta) > 0.0001f)
        {
            var worldBeforeZoom = ScreenToWorld(zoomAnchor);
            Zoom = Math.Clamp(Zoom + zoomDelta * ZoomSpeed * Zoom, MinZoom, MaxZoom);
            var worldAfterZoom = ScreenToWorld(zoomAnchor);
            var tileSize = BaseTileSize * Zoom;
            Offset += (worldAfterZoom - worldBeforeZoom) * tileSize;
        }
    }

    /// <summary>
    /// Convert a screen position to world tile coordinates.
    /// </summary>
    public Vector2 ScreenToWorld(Vector2 screenPos)
    {
        float tileSize = BaseTileSize * Zoom;
        return new Vector2(
            (screenPos.X - Offset.X) / tileSize,
            (screenPos.Y - Offset.Y) / tileSize
        );
    }
}
