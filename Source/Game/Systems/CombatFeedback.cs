namespace Game.Systems;

public sealed class CombatFeedback : ICombatFeedback
{
    private readonly SoundSystem _soundSystem;
    private readonly EffectSystem _effectSystem;

    public CombatFeedback(SoundSystem soundSystem, EffectSystem effectSystem)
    {
        _soundSystem = soundSystem;
        _effectSystem = effectSystem;
    }

    public void OnEnemyFired() => _soundSystem.PlayEnemyFire();

    public void OnPlayerDamaged(float amount) => _effectSystem.TriggerDamageFlash();
}
