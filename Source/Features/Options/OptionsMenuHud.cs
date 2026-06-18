using Game.Core.Level;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Options;

/// <summary>Immediate-mode Raylib pause / options overlay.</summary>
public static class OptionsMenuHud
{
    private const int LabelSize = 20;
    private const int TitleSize = 36;
    private const int HintSize = 16;
    private static readonly Color DisabledColor = new(120, 120, 120, 255);

    public static void Draw(GameSettings settings, int screenWidth, int screenHeight)
    {
        DrawRectangle(0, 0, screenWidth, screenHeight, new Color(0, 0, 0, 160));

        var layout = OptionsMenuLayout.Compute(screenWidth, screenHeight);
        DrawRectangle(layout.PanelX, layout.PanelY, layout.PanelW, layout.PanelH, new Color(20, 20, 20, 240));
        DrawRectangleLines(layout.PanelX, layout.PanelY, layout.PanelW, layout.PanelH, new Color(180, 180, 180, 255));

        const string title = "OPTIONS";
        int titleW = MeasureText(title, TitleSize);
        DrawText(title, (screenWidth - titleW) / 2, layout.PanelY + 16, TitleSize, Color.RayWhite);

        int contentX = layout.PanelX + 32;
        var labelColor = new Color(200, 200, 200, 255);
        var valueColor = Color.RayWhite;
        int centerX = layout.PanelX + layout.PanelW / 2;

        var (monitorW, monitorH) = WindowDisplayMode.GetReferenceDimensions();
        int windowW = (int)RenderData.Resolution.X;
        int windowH = (int)RenderData.Resolution.Y;

        DrawText("Fullscreen", contentX, layout.PanelY + 74, LabelSize, labelColor);
        DrawCheckbox(layout.FullscreenCheckbox, settings.FullscreenEnabled);

        bool windowRowEnabled = !settings.FullscreenEnabled && !OperatingSystem.IsBrowser();
        DrawText("Window resolution", contentX, layout.PanelY + 120, LabelSize, windowRowEnabled ? labelColor : DisabledColor);
        if (windowRowEnabled)
        {
            DrawButton(layout.WindowResolutionPrev, "<");
            DrawButton(layout.WindowResolutionNext, ">");
            var windowPreset = KnownResolutions.FindById(settings.WindowResolutionPresetId);
            string windowLabel = KnownResolutions.FormatLabel(windowPreset, monitorW, monitorH);
            DrawCenteredValue(windowLabel, centerX, layout.PanelY + 128, LabelSize, valueColor);
        }
        else if (settings.FullscreenEnabled)
        {
            string fullscreenLabel = $"Monitor ({windowW} x {windowH})";
            DrawCenteredValue(fullscreenLabel, centerX, layout.PanelY + 128, LabelSize, DisabledColor);
        }
        else
        {
            DrawCenteredValue("Browser viewport", centerX, layout.PanelY + 128, LabelSize, DisabledColor);
        }

        DrawText("Game resolution", contentX, layout.PanelY + 166, LabelSize, labelColor);
        DrawButton(layout.GameResolutionPrev, "<");
        DrawButton(layout.GameResolutionNext, ">");
        var gamePreset = KnownResolutions.FindById(settings.GameResolutionPresetId);
        string gameLabel = KnownResolutions.FormatLabel(gamePreset, windowW, windowH);
        DrawCenteredValue(gameLabel, centerX, layout.PanelY + 174, LabelSize, valueColor);

        DrawText("VSync", contentX, layout.PanelY + 220, LabelSize, labelColor);
        DrawCheckbox(layout.VSyncCheckbox, settings.VSyncEnabled);

        DrawText("FPS limit", contentX, layout.PanelY + 270, LabelSize, settings.VSyncEnabled ? DisabledColor : labelColor);
        DrawFpsSlider(layout.FpsSliderTrack, settings.TargetFps, enabled: !settings.VSyncEnabled);

        string fpsValue = settings.VSyncEnabled ? "display" : settings.TargetFps.ToString();
        DrawText(fpsValue, (int)(layout.FpsSliderTrack.X + layout.FpsSliderTrack.Width + 12), layout.PanelY + 266, LabelSize,
            settings.VSyncEnabled ? DisabledColor : valueColor);

        int hintY = layout.PanelY + layout.PanelH - 88;
        DrawCenteredLine("Left/Right: game res  |  Shift+Left/Right: window res", screenWidth, hintY, HintSize, DisabledColor);
        DrawCenteredLine("ESC - Resume  |  Q - Quit", screenWidth, hintY + 24, HintSize, Color.RayWhite);
    }

    private static void DrawButton(Rectangle rect, string label)
    {
        DrawRectangleRec(rect, new Color(50, 50, 50, 255));
        DrawRectangleLinesEx(rect, 1f, new Color(140, 140, 140, 255));
        int w = MeasureText(label, LabelSize);
        DrawText(label, (int)(rect.X + (rect.Width - w) / 2), (int)(rect.Y + 6), LabelSize, Color.RayWhite);
    }

    private static void DrawCheckbox(Rectangle rect, bool enabled)
    {
        DrawRectangleRec(rect, new Color(40, 40, 40, 255));
        DrawRectangleLinesEx(rect, 1f, new Color(140, 140, 140, 255));
        if (enabled)
            DrawText("X", (int)rect.X + 5, (int)rect.Y + 2, LabelSize, new Color(120, 220, 120, 255));
    }

    private static void DrawFpsSlider(Rectangle track, int fps, bool enabled)
    {
        DrawRectangleRec(track, enabled ? new Color(50, 50, 50, 255) : new Color(35, 35, 35, 255));

        float t = (fps - GraphicsFramePacing.MinTargetFps) /
            (float)(GraphicsFramePacing.MaxTargetFps - GraphicsFramePacing.MinTargetFps);
        t = Math.Clamp(t, 0f, 1f);

        int thumbW = 12;
        int thumbX = (int)(track.X + t * (track.Width - thumbW));
        var thumbColor = enabled ? Color.RayWhite : DisabledColor;
        DrawRectangle(thumbX, (int)track.Y - 2, thumbW, (int)track.Height + 4, thumbColor);
    }

    private static void DrawCenteredValue(string text, int centerX, int y, int size, Color color)
    {
        int w = MeasureText(text, size);
        DrawText(text, centerX - w / 2, y, size, color);
    }

    private static void DrawCenteredLine(string text, int screenWidth, int y, int size, Color color)
    {
        int w = MeasureText(text, size);
        DrawText(text, (screenWidth - w) / 2, y, size, color);
    }
}
