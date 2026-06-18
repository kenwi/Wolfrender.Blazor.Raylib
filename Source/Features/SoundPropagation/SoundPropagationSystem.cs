using System.Numerics;
using Game.Features.Doors;

namespace Game.Features.SoundPropagation;

/// <summary>
/// Runtime sound emission and reach computation. Gunshots enqueue <see cref="SoundEvent"/>s
/// for enemy hearing systems to consume each frame.
/// </summary>
public sealed class SoundPropagationSystem
{
    private readonly MapData _mapData;
    private readonly DoorSystem _doorSystem;
    private readonly List<SoundEvent> _pendingEvents = new();

    public SoundPropagationSystem(MapData mapData, DoorSystem doorSystem)
    {
        _mapData = mapData;
        _doorSystem = doorSystem;
    }

    public IReadOnlyList<SoundEvent> PendingEvents => _pendingEvents;

    public void ClearPendingEvents() => _pendingEvents.Clear();

    /// <summary>Emit a gunshot from the player's world position (converted to tile coords).</summary>
    public void EmitPlayerGunshot(Vector3 playerWorldPosition)
    {
        int originX = (int)MathF.Floor(playerWorldPosition.X / LevelData.QuadSize);
        int originY = (int)MathF.Floor(playerWorldPosition.Z / LevelData.QuadSize);
        EmitGunshotAtTile(originX, originY);
    }

    /// <summary>Emit a gunshot from an explicit tile origin using live door states.</summary>
    public void EmitGunshotAtTile(int originX, int originY)
    {
        var reach = SoundPropagation.ComputeReach(
            _mapData, _doorSystem.Doors, originX, originY);

        if (reach.Count == 0)
            return;

        _pendingEvents.Add(new SoundEvent
        {
            OriginX = originX,
            OriginY = originY,
            ReachedTiles = reach,
        });
    }
}
