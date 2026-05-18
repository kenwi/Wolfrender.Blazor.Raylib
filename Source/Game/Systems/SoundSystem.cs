using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Systems;

public class SoundSystem
{
    private Music _music;
    private float _volume = 0.2f;
    private Sound? _enemyFireSound;
    private bool _enemyFireSoundReady;

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
        if (_enemyFireSound.HasValue)
            SetSoundVolume(_enemyFireSound.Value, _volume);
    }

    public float GetVolume() => _volume;

    /// <summary>Short gunshot SFX when an enemy fires (<c>wwwroot/resources/enemy_fire.wav</c>).</summary>
    public void PlayEnemyFire()
    {
        if (!_enemyFireSoundReady)
        {
            _enemyFireSound = LoadSound(Utilities.Res.Path("resources/EnemyPistolFire.ogg"));
            SetSoundVolume(_enemyFireSound.Value, _volume);
            _enemyFireSoundReady = true;
        }

        PlaySound(_enemyFireSound!.Value);
    }
}
