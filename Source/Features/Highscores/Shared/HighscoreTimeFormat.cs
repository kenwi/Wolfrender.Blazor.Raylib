using System.Globalization;

namespace Game.Features.Highscores.Shared;

public static class HighscoreTimeFormat
{
    public static string Format(float elapsedSeconds)
    {
        int minutes = (int)(elapsedSeconds / 60f);
        float seconds = elapsedSeconds - minutes * 60f;
        return string.Create(CultureInfo.InvariantCulture, $"{minutes}:{seconds:00.00}");
    }
}
