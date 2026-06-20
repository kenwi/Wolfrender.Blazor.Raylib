using Game.Features.Options;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Engine.Audio;

public class SoundSystem
{
    private Music _music;
    private float _musicLevel = AudioVolumeLevel.Default;
    private float _sfxLevel = AudioVolumeLevel.Default;
    private readonly Dictionary<string, Sound> _soundsByPath = new();

    public SoundSystem(string musicPath)
    {
        _music = LoadMusicStream(musicPath);
        PlayMusicStream(_music);
        SetMusicLevel(_musicLevel);
        SetSfxLevel(_sfxLevel);
    }

    public void Update()
    {
        UpdateMusicStream(_music);
    }

    public void SetMusicLevel(float level)
    {
        _musicLevel = AudioVolumeLevel.Clamp(level);
        SetMusicVolume(_music, AudioVolumeLevel.ToRaylibVolume(_musicLevel));
    }

    public void SetSfxLevel(float level)
    {
        _sfxLevel = AudioVolumeLevel.Clamp(level);
        float volume = AudioVolumeLevel.ToRaylibVolume(_sfxLevel);
        foreach (var sound in _soundsByPath.Values)
            SetSoundVolume(sound, volume);
    }

    public float GetMusicLevel() => _musicLevel;
    public float GetSfxLevel() => _sfxLevel;

    public void SetVolume(float volume)
    {
        float level = AudioVolumeLevel.FromSliderPosition(Math.Clamp(volume, 0f, 1f));
        SetMusicLevel(level);
        SetSfxLevel(level);
    }

    public float GetVolume() => AudioVolumeLevel.ToRaylibVolume(_sfxLevel);

    /// <summary>Play a one-shot SFX by resource path, loading and caching it on first use.</summary>
    public void PlaySfx(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (!_soundsByPath.TryGetValue(path, out var sound))
        {
            sound = LoadSound(Res.Path(path));
            SetSoundVolume(sound, AudioVolumeLevel.ToRaylibVolume(_sfxLevel));
            _soundsByPath[path] = sound;
        }

        PlaySound(sound);
    }
}
