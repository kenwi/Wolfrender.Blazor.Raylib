using System.Numerics;
using Game.Features.Doors;
using Game.Features.Enemies;
using Game.Features.LevelProgress;
using Game.Features.Players;

namespace Game.Features.Recording;

/// <summary>
/// Deterministic FNV-1a hashing of gameplay state, captured at the end of a simulation tick.
/// Uses raw float bits so identical simulations produce identical hashes.
/// </summary>
public static class SimulationChecksum
{
    /// <summary>Keyframe every second at the default 60 Hz tick rate.</summary>
    public const int KeyframeIntervalTicks = 60;

    private const uint FnvOffset = 2166136261;
    private const uint FnvPrime = 16777619;

    public static bool IsKeyframeTick(long tick) =>
        tick > 0 && tick % KeyframeIntervalTicks == 0;

    public static ChecksumKeyframe Capture(
        long tick,
        Player player,
        IReadOnlyList<Enemy> enemies,
        IReadOnlyList<Door> doors,
        IScoreSnapshot score)
    {
        return new ChecksumKeyframe(
            tick,
            HashPlayer(player),
            HashEnemies(enemies),
            HashDoors(doors),
            HashScore(score));
    }

    private static uint HashPlayer(Player player)
    {
        uint h = FnvOffset;
        h = Mix(h, player.Position);
        h = Mix(h, player.Velocity);
        h = Mix(h, player.Camera.Target - player.Camera.Position);
        h = Mix(h, player.Health);
        h = Mix(h, (uint)player.Ammo);
        return h;
    }

    private static uint HashEnemies(IReadOnlyList<Enemy> enemies)
    {
        uint h = FnvOffset;
        for (int i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            h = Mix(h, enemy.Position);
            h = Mix(h, enemy.Rotation);
            h = Mix(h, enemy.Health);
            h = Mix(h, (uint)enemy.EnemyState);
            h = Mix(h, enemy.StateTimer);
        }

        return h;
    }

    private static uint HashDoors(IReadOnlyList<Door> doors)
    {
        uint h = FnvOffset;
        for (int i = 0; i < doors.Count; i++)
        {
            var door = doors[i];
            h = Mix(h, door.Position.X);
            h = Mix(h, door.Position.Y);
            h = Mix(h, (uint)door.DoorState);
        }

        return h;
    }

    private static uint HashScore(IScoreSnapshot score)
    {
        uint h = FnvOffset;
        h = Mix(h, (uint)score.LevelScore);
        h = Mix(h, (uint)score.Kills);
        h = Mix(h, (uint)score.TreasuresCollected);
        h = Mix(h, (uint)score.SecretsFound);
        h = Mix(h, score.ElapsedActiveSeconds);
        return h;
    }

    private static uint Mix(uint hash, Vector3 value)
    {
        hash = Mix(hash, value.X);
        hash = Mix(hash, value.Y);
        return Mix(hash, value.Z);
    }

    private static uint Mix(uint hash, float value) =>
        Mix(hash, BitConverter.SingleToUInt32Bits(value));

    private static uint Mix(uint hash, uint value) =>
        (hash ^ value) * FnvPrime;
}
