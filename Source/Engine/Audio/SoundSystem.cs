using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Engine.Audio;

public class SoundSystem
{
    private Music _music;
    private float _volume = 0.2f;
    private readonly Dictionary<string, Sound> _soundsByPath = new();

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
        foreach (var sound in _soundsByPath.Values)
            SetSoundVolume(sound, _volume);
    }

    public float GetVolume() => _volume;

    /// <summary>Play a one-shot SFX by resource path, loading and caching it on first use.</summary>
    public void PlaySfx(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (!_soundsByPath.TryGetValue(path, out var sound))
        {
            sound = LoadSound(Res.Path(path));
            SetSoundVolume(sound, _volume);
            _soundsByPath[path] = sound;
        }

        PlaySound(sound);
    }
}
