using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Systems;

public class SoundSystem
{
    private Music _music;

    public SoundSystem(string musicPath)
    {
        _music = LoadMusicStream(musicPath);
        PlayMusicStream(_music);
    }

    public void Update()
    {
        UpdateMusicStream(_music);
    }

    public void SetVolume(float volume)
    {
        SetMusicVolume(_music, Math.Clamp(volume, 0f, 1f));
    }
}
