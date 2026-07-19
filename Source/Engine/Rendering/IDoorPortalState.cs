namespace Game.Engine.Rendering;

/// <summary>
/// Runtime door openness for light occlusion and room visibility.
/// Implemented by Features/Doors (<c>DoorSystem</c>).
/// </summary>
public interface IDoorPortalState
{
    /// <summary>True when the door at the tile is closed, or no door entity exists there.</summary>
    bool IsClosedAt(int tileX, int tileY);

    /// <summary>True when the door at the tile is not closed (opening/open/closing).</summary>
    bool IsPassableAt(int tileX, int tileY);
}
