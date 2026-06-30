using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Hud;

public static class RecordingStatusHud
{
    private const int FontSize = 24;

    private static readonly Color RecColor = new(255, 48, 48, 255);
    private static readonly Color ReplayColor = new(96, 192, 255, 255);

    public static void Draw(bool isRecording, bool isReplaying, int screenWidth, int y = 10)
    {
        if (!isRecording && !isReplaying)
            return;

        string label = isRecording ? "REC" : "REPLAY";
        Color color = isRecording ? RecColor : ReplayColor;
        int width = MeasureText(label, FontSize);
        DrawText(label, (screenWidth - width) / 2, y, FontSize, color);
    }
}
