using Raylib_cs;

namespace Game.Engine.Rendering;

/// <summary>Deterministic translucent colors for room debug overlays in the editor.</summary>
public static class RoomOverlayColors
{
    private static readonly int transparentAlpha = 90;
    private static readonly Color[] Palette =
    {
        new(255, 99, 132, transparentAlpha),
        new(54, 162, 235, transparentAlpha),
        new(255, 206, 86, transparentAlpha),
        new(75, 192, 192, transparentAlpha),
        new(153, 102, 255, transparentAlpha),
        new(255, 159, 64, transparentAlpha),
        new(199, 199, 199, transparentAlpha),
        new(83, 102, 255, transparentAlpha),
        new(255, 99, 255, transparentAlpha),
        new(99, 255, 132, transparentAlpha),
        new(255, 140, 180, transparentAlpha),
        new(120, 220, 255, transparentAlpha),
    };

    public static Color ForRoom(int roomId) => Palette[Math.Abs(roomId) % Palette.Length];
}
