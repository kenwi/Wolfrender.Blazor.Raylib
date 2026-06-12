namespace Game.Features.Combat;

public sealed class CombatFeedback : ICombatFeedback
{
    private const string EnemyFireSoundPath = "resources/EnemyPistolFire.ogg";

    private readonly SoundSystem _soundSystem;
    private readonly EffectSystem _effectSystem;

    public CombatFeedback(SoundSystem soundSystem, EffectSystem effectSystem)
    {
        _soundSystem = soundSystem;
        _effectSystem = effectSystem;
    }

    public void OnEnemyFired() => _soundSystem.PlaySfx(EnemyFireSoundPath);

    public void OnPlayerDamaged(float amount) => _effectSystem.TriggerDamageFlash();
}
