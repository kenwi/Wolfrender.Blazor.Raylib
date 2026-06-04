namespace Game.Engine.Audio;

/// <summary>Audio/visual feedback for combat events (keeps gameplay systems decoupled from Raylib).</summary>
public interface ICombatFeedback
{
    void OnEnemyFired();
    void OnPlayerDamaged(float amount);
}
