using System.Numerics;
using Raylib_cs;

namespace Game.Features.Combat;

/// <summary>
/// Shootable combat participant. Owned by Combat so hitscan stays free of concrete Enemy.
/// </summary>
public interface ICombatTarget
{
    Vector3 Position { get; }
    bool IsCombatActive { get; }
    Rectangle FrameRect { get; }
    void ApplyDamage(float amount);
}
