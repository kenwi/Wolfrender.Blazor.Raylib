using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Combat;

/// <summary>Timed full-screen and HUD visual feedback (damage flash, reticle pulse).</summary>
public class EffectSystem
{
    private const float DamageFlashDuration = 0.28f;
    private const float ReticleFireFlashDuration = 0.09f;

    private float _damageFlashRemaining;
    private float _reticleFireFlashRemaining;
    private bool _deathOverlayActive;

    public void Update(float deltaTime)
    {
        if (_damageFlashRemaining > 0f)
            _damageFlashRemaining = MathF.Max(0f, _damageFlashRemaining - deltaTime);
        if (_reticleFireFlashRemaining > 0f)
            _reticleFireFlashRemaining = MathF.Max(0f, _reticleFireFlashRemaining - deltaTime);
    }

    public void Clear()
    {
        _damageFlashRemaining = 0f;
        _reticleFireFlashRemaining = 0f;
        _deathOverlayActive = false;
    }

    public void EnableDeathOverlay() => _deathOverlayActive = true;

    public void TriggerDamageFlash() => _damageFlashRemaining = DamageFlashDuration;

    public void TriggerReticleFireFlash() => _reticleFireFlashRemaining = ReticleFireFlashDuration;

    public Color GetReticleColor()
    {
        return _reticleFireFlashRemaining > 0f
            ? new Color(255, 55, 55, 255)
            : new Color(235, 235, 210, 255);
    }

    public void RenderScreenOverlay(int screenWidth, int screenHeight)
    {
        if (_deathOverlayActive)
        {
            DrawRectangle(0, 0, screenWidth, screenHeight, new Color((byte)200, (byte)20, (byte)20, (byte)115));
            return;
        }

        if (_damageFlashRemaining <= 0f)
            return;

        float t = _damageFlashRemaining / DamageFlashDuration;
        byte alpha = (byte)(140 * t);
        DrawRectangle(0, 0, screenWidth, screenHeight, new Color((byte)200, (byte)20, (byte)20, alpha));
    }
}
