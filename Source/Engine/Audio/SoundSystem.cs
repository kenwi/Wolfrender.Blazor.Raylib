using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Engine.Audio;

/// <summary>
/// Music and one-shot SFX. Volumes are Raylib gains in 0–1; Options converts UI levels before calling in.
/// </summary>
public class SoundSystem
{
    /// <summary>Matches Options default level 1 of 3 when no settings have been applied yet.</summary>
    private const float DefaultRaylibVolume = 1f / 3f;

    private Music _music;
    private float _musicVolume = DefaultRaylibVolume;
    private float _sfxVolume = DefaultRaylibVolume;
    private readonly Dictionary<string, Sound> _soundsByPath = new();

    public SoundSystem(string musicPath)
    {
        _music = LoadMusicStream(musicPath);
        PlayMusicStream(_music);
        ApplyMusicVolume(_musicVolume);
        ApplySfxVolume(_sfxVolume);
    }

    public void Update()
    {
        UpdateMusicStream(_music);
    }

    public void ApplyMusicVolume(float raylibVolume)
    {
        _musicVolume = Math.Clamp(raylibVolume, 0f, 1f);
        SetMusicVolume(_music, _musicVolume);
    }

    public void ApplySfxVolume(float raylibVolume)
    {
        _sfxVolume = Math.Clamp(raylibVolume, 0f, 1f);
        foreach (var sound in _soundsByPath.Values)
            SetSoundVolume(sound, _sfxVolume);
    }

    public float GetMusicVolume() => _musicVolume;
    public float GetSfxVolume() => _sfxVolume;

    /// <summary>Play a one-shot SFX by resource path, loading and caching it on first use.</summary>
    public void PlaySfx(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (!_soundsByPath.TryGetValue(path, out var sound))
        {
            sound = LoadSound(Res.Path(path));
            SetSoundVolume(sound, _sfxVolume);
            _soundsByPath[path] = sound;
        }

        PlaySound(sound);
    }
}
