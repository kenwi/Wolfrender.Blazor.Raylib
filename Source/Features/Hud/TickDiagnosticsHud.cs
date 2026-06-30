using Game.Engine.Simulation;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Hud;

public static class TickDiagnosticsHud
{
    private const int FontSize = 18;
    private const int LineSpacing = 4;
    /// <summary>Below health (40) and combat inventory block (68..~154).</summary>
    public const int DefaultStartY = 168;

    private static readonly Color TextColor = new(120, 255, 160, 255);
    private static readonly Color WarnColor = new(255, 200, 80, 255);

    public static void Draw(TickDiagnostics diagnostics, int x = 10, int startY = DefaultStartY)
    {
        if (!diagnostics.OverlayEnabled)
            return;

        int y = startY;
        DrawLine($"Render: {diagnostics.RenderFps:F1} fps", x, ref y);
        DrawLine($"Sim: {diagnostics.SimHz:F0} Hz (target {diagnostics.TickHz})", x, ref y);
        DrawLine($"Tick: {diagnostics.SimTickIndex}", x, ref y);
        DrawLine($"Alpha: {diagnostics.InterpolationAlpha:F2}", x, ref y);
        DrawLine($"Ticks/frame: {diagnostics.LastTicksThisFrame}", x, ref y);
        DrawLine($"FixedDt: {diagnostics.FixedDeltaTimeMs:F2} ms", x, ref y);

        if (diagnostics.HitTickCap)
            DrawLine("Tick cap hit (sim behind)", x, ref y, WarnColor);
    }

    private static void DrawLine(string text, int x, ref int y, Color? color = null)
    {
        DrawText(text, x, y, FontSize, color ?? TextColor);
        y += FontSize + LineSpacing;
    }
}
