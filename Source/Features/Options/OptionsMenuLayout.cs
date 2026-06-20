using System.Numerics;
using Raylib_cs;

namespace Game.Features.Options;

public readonly struct OptionsMenuLayout
{
    public int PanelX { get; init; }
    public int PanelY { get; init; }
    public int PanelW { get; init; }
    public int PanelH { get; init; }
    public Rectangle FullscreenCheckbox { get; init; }
    public Rectangle WindowResolutionPrev { get; init; }
    public Rectangle WindowResolutionNext { get; init; }
    public Rectangle GameResolutionPrev { get; init; }
    public Rectangle GameResolutionNext { get; init; }
    public Rectangle VSyncCheckbox { get; init; }
    public Rectangle FpsSliderTrack { get; init; }
    public Rectangle MouseSensitivityPrev { get; init; }
    public Rectangle MouseSensitivityNext { get; init; }
    public Rectangle AudioSliderTrack { get; init; }
    public Rectangle MusicSliderTrack { get; init; }

    public static OptionsMenuLayout Compute(int screenWidth, int screenHeight)
    {
        const int panelW = 800;
        const int panelH = 520;
        int panelX = (screenWidth - panelW) / 2;
        int panelY = (screenHeight - panelH) / 2;
        int contentX = panelX + 32;
        int rowW = panelW - 64;

        int fullscreenY = panelY + 72;
        int windowY = panelY + 118;
        int gameY = panelY + 164;
        int vsyncY = panelY + 210;
        int fpsY = panelY + 256;
        int mouseY = panelY + 302;
        int audioY = panelY + 348;
        int musicY = panelY + 394;

        return new OptionsMenuLayout
        {
            PanelX = panelX,
            PanelY = panelY,
            PanelW = panelW,
            PanelH = panelH,
            FullscreenCheckbox = new Rectangle(contentX, fullscreenY, 24, 24),
            WindowResolutionPrev = new Rectangle(contentX, windowY, 36, 32),
            WindowResolutionNext = new Rectangle(contentX + rowW - 36, windowY, 36, 32),
            GameResolutionPrev = new Rectangle(contentX, gameY, 36, 32),
            GameResolutionNext = new Rectangle(contentX + rowW - 36, gameY, 36, 32),
            VSyncCheckbox = new Rectangle(contentX, vsyncY, 24, 24),
            FpsSliderTrack = new Rectangle(contentX + 250, fpsY + 4, rowW - 350, 16),
            MouseSensitivityPrev = new Rectangle(contentX, mouseY, 36, 32),
            MouseSensitivityNext = new Rectangle(contentX + rowW - 36, mouseY, 36, 32),
            AudioSliderTrack = new Rectangle(contentX + 250, audioY + 4, rowW - 350, 16),
            MusicSliderTrack = new Rectangle(contentX + 250, musicY + 4, rowW - 350, 16),
        };
    }

    public static bool Contains(Rectangle rect, Vector2 point) =>
        point.X >= rect.X && point.X <= rect.X + rect.Width &&
        point.Y >= rect.Y && point.Y <= rect.Y + rect.Height;
}
