using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Systems;

public class SoundSystem
{
    private Music _music;
    private float _volume = 0.2f;
    private Sound? _enemyFireSound;
    private bool _enemyFireSoundReady;
    private Sound? _pistolFireSound;
    private bool _pistolFireSoundReady;

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
        if (_pistolFireSound.HasValue)
            SetSoundVolume(_pistolFireSound.Value, _volume);
    }

    public float GetVolume() => _volume;

    /// <summary>Short gunshot SFX when an enemy fires (<c>resources/EnemyPistolFire.ogg</c>).</summary>
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

    /// <summary>Player pistol shot (<c>resources/PistolFire.ogg</c>).</summary>
    public void PlayPistolFire()
    {
        if (!_pistolFireSoundReady)
        {
            _pistolFireSound = LoadSound(Utilities.Res.Path("resources/PistolFire.ogg"));
            SetSoundVolume(_pistolFireSound.Value, _volume);
            _pistolFireSoundReady = true;
        }

        PlaySound(_pistolFireSound!.Value);
    }
}
