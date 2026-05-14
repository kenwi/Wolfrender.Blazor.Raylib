using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Systems;

public class SoundSystem
{
    private Music _music;
    private float _volume = 1f;

    public SoundSystem(string musicPath)
    {
        _music = LoadMusicStream(musicPath);
        PlayMusicStream(_music);
        SetVolume(_volume);
    }

    public void Update()
    {
        UpdateMusicStream(_music);
    }

    public void SetVolume(float volume)
    {
        _volume = Math.Clamp(volume, 0f, 1f);
        SetMusicVolume(_music, _volume);
    }

    public float GetVolume() => _volume;
}
