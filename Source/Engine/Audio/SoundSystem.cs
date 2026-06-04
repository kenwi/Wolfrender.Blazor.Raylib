using Game.Features.Combat;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Engine.Audio;

public class SoundSystem
{
    private Music _music;
    private float _volume = 0.2f;
    private Sound? _enemyFireSound;
    private bool _enemyFireSoundReady;
    private readonly Dictionary<string, Sound> _weaponSoundsByPath = new();

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
        foreach (var sound in _weaponSoundsByPath.Values)
            SetSoundVolume(sound, _volume);
    }

    public float GetVolume() => _volume;

    /// <summary>Short gunshot SFX when an enemy fires (<c>resources/EnemyPistolFire.ogg</c>).</summary>
    public void PlayEnemyFire()
    {
        if (!_enemyFireSoundReady)
        {
            _enemyFireSound = LoadSound(Res.Path("resources/EnemyPistolFire.ogg"));
            SetSoundVolume(_enemyFireSound.Value, _volume);
            _enemyFireSoundReady = true;
        }

        PlaySound(_enemyFireSound!.Value);
    }

    /// <summary>Player pistol shot (<c>resources/PistolFire.ogg</c>).</summary>
    public void PlayPistolFire() => PlayWeaponFire(WeaponId.Pistol);

    public void PlayWeaponFire(WeaponId weaponId)
    {
        string path = WeaponCatalog.Get(weaponId).FireSoundPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (!_weaponSoundsByPath.TryGetValue(path, out var sound))
        {
            sound = LoadSound(Res.Path(path));
            SetSoundVolume(sound, _volume);
            _weaponSoundsByPath[path] = sound;
        }

        PlaySound(sound);
    }
}
