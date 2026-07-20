using System.Numerics;

namespace Game.Features.Doors;

/// <summary>
/// Occupancy probe over a primary actor plus additional world positions (e.g. enemies).
/// Call <see cref="BeginFrame"/> each tick before <see cref="DoorSystem.Update"/>.
/// </summary>
public sealed class ActorDoorOccupancyProbe : IDoorOccupancyProbe
{
    private readonly float _quadSize;
    private Vector3 _primaryWorldPosition;
    private IReadOnlyList<Vector3> _otherWorldPositions = Array.Empty<Vector3>();

    public ActorDoorOccupancyProbe(float? quadSize = null)
    {
        _quadSize = quadSize ?? LevelData.QuadSize;
    }

    public void BeginFrame(Vector3 primaryWorldPosition, IReadOnlyList<Vector3> otherWorldPositions)
    {
        _primaryWorldPosition = primaryWorldPosition;
        _otherWorldPositions = otherWorldPositions;
    }

    public bool IsTileOccupied(int tileX, int tileY)
    {
        if (WorldToTile(_primaryWorldPosition) == (tileX, tileY))
            return true;

        for (int i = 0; i < _otherWorldPositions.Count; i++)
        {
            if (WorldToTile(_otherWorldPositions[i]) == (tileX, tileY))
                return true;
        }

        return false;
    }

    private (int X, int Y) WorldToTile(Vector3 world) =>
        ((int)(world.X / _quadSize + 0.5f), (int)(world.Z / _quadSize + 0.5f));
}
