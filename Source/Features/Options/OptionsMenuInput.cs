using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Options;

public static class OptionsMenuInput
{
    public readonly struct Result
    {
        public bool WindowDisplayChanged { get; init; }
        public bool GameResolutionChanged { get; init; }
        public bool GraphicsChanged { get; init; }
        public bool ControlsChanged { get; init; }
        public bool AudioChanged { get; init; }
    }

    public static Result Update(GameSettings settings, int screenWidth, int screenHeight)
    {
        var layout = OptionsMenuLayout.Compute(screenWidth, screenHeight);
        bool windowDisplayChanged = false;
        bool gameResolutionChanged = false;
        bool graphicsChanged = false;
        bool controlsChanged = false;
        bool audioChanged = false;
        bool shift = IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift);

        if (IsKeyPressed(KeyboardKey.F))
        {
            settings.FullscreenEnabled = !settings.FullscreenEnabled;
            windowDisplayChanged = true;
        }

        if (!settings.FullscreenEnabled)
        {
            if (shift && IsKeyPressed(KeyboardKey.Left))
            {
                settings.WindowResolutionPresetId = KnownResolutions.CycleId(settings.WindowResolutionPresetId, -1);
                windowDisplayChanged = true;
            }

            if (shift && IsKeyPressed(KeyboardKey.Right))
            {
                settings.WindowResolutionPresetId = KnownResolutions.CycleId(settings.WindowResolutionPresetId, 1);
                windowDisplayChanged = true;
            }
        }

        if (!shift && IsKeyPressed(KeyboardKey.Left))
        {
            settings.GameResolutionPresetId = KnownResolutions.CycleId(settings.GameResolutionPresetId, -1);
            gameResolutionChanged = true;
        }

        if (!shift && IsKeyPressed(KeyboardKey.Right))
        {
            settings.GameResolutionPresetId = KnownResolutions.CycleId(settings.GameResolutionPresetId, 1);
            gameResolutionChanged = true;
        }

        if (IsKeyPressed(KeyboardKey.Space))
        {
            settings.VSyncEnabled = !settings.VSyncEnabled;
            graphicsChanged = true;
        }

        if (!settings.VSyncEnabled)
        {
            if (IsKeyPressed(KeyboardKey.Up))
            {
                settings.TargetFps = GraphicsFramePacing.ClampTargetFps(settings.TargetFps + 5);
                graphicsChanged = true;
            }

            if (IsKeyPressed(KeyboardKey.Down))
            {
                settings.TargetFps = GraphicsFramePacing.ClampTargetFps(settings.TargetFps - 5);
                graphicsChanged = true;
            }
        }

        var mouse = GetMousePosition();
        bool click = IsMouseButtonPressed(MouseButton.Left);
        bool drag = IsMouseButtonDown(MouseButton.Left);

        if (click && OptionsMenuLayout.Contains(layout.FullscreenCheckbox, mouse))
        {
            settings.FullscreenEnabled = !settings.FullscreenEnabled;
            windowDisplayChanged = true;
        }

        if (!settings.FullscreenEnabled)
        {
            if (click && OptionsMenuLayout.Contains(layout.WindowResolutionPrev, mouse))
            {
                settings.WindowResolutionPresetId = KnownResolutions.CycleId(settings.WindowResolutionPresetId, -1);
                windowDisplayChanged = true;
            }

            if (click && OptionsMenuLayout.Contains(layout.WindowResolutionNext, mouse))
            {
                settings.WindowResolutionPresetId = KnownResolutions.CycleId(settings.WindowResolutionPresetId, 1);
                windowDisplayChanged = true;
            }
        }

        if (click && OptionsMenuLayout.Contains(layout.GameResolutionPrev, mouse))
        {
            settings.GameResolutionPresetId = KnownResolutions.CycleId(settings.GameResolutionPresetId, -1);
            gameResolutionChanged = true;
        }

        if (click && OptionsMenuLayout.Contains(layout.GameResolutionNext, mouse))
        {
            settings.GameResolutionPresetId = KnownResolutions.CycleId(settings.GameResolutionPresetId, 1);
            gameResolutionChanged = true;
        }

        if (click && OptionsMenuLayout.Contains(layout.VSyncCheckbox, mouse))
        {
            settings.VSyncEnabled = !settings.VSyncEnabled;
            graphicsChanged = true;
        }

        if ((click || drag) && OptionsMenuLayout.Contains(layout.FpsSliderTrack, mouse))
        {
            if (settings.VSyncEnabled && click)
                settings.VSyncEnabled = false;

            if (!settings.VSyncEnabled)
            {
                float t = (mouse.X - layout.FpsSliderTrack.X) / layout.FpsSliderTrack.Width;
                t = Math.Clamp(t, 0f, 1f);
                int fps = GraphicsFramePacing.MinTargetFps +
                    (int)MathF.Round(t * (GraphicsFramePacing.MaxTargetFps - GraphicsFramePacing.MinTargetFps));
                settings.TargetFps = GraphicsFramePacing.ClampTargetFps(fps);
            }

            graphicsChanged = true;
        }

        if (click && OptionsMenuLayout.Contains(layout.MouseSensitivityPrev, mouse))
        {
            settings.MouseSensitivity = MouseSensitivitySetting.Adjust(settings.MouseSensitivity, -1);
            controlsChanged = true;
        }

        if (click && OptionsMenuLayout.Contains(layout.MouseSensitivityNext, mouse))
        {
            settings.MouseSensitivity = MouseSensitivitySetting.Adjust(settings.MouseSensitivity, 1);
            controlsChanged = true;
        }

        if ((click || drag) && OptionsMenuLayout.Contains(layout.AudioSliderTrack, mouse))
        {
            float t = (mouse.X - layout.AudioSliderTrack.X) / layout.AudioSliderTrack.Width;
            settings.AudioLevel = AudioVolumeLevel.FromSliderPosition(t);
            audioChanged = true;
        }

        if ((click || drag) && OptionsMenuLayout.Contains(layout.MusicSliderTrack, mouse))
        {
            float t = (mouse.X - layout.MusicSliderTrack.X) / layout.MusicSliderTrack.Width;
            settings.MusicLevel = AudioVolumeLevel.FromSliderPosition(t);
            audioChanged = true;
        }

        return new Result
        {
            WindowDisplayChanged = windowDisplayChanged,
            GameResolutionChanged = gameResolutionChanged,
            GraphicsChanged = graphicsChanged,
            ControlsChanged = controlsChanged,
            AudioChanged = audioChanged,
        };
    }
}
