using System.Numerics;

namespace Game.Entities;

public enum DoorRotation
{
    HORIZONTAL,
    VERTICAL
}

public enum DoorState
{
    OPENING,
    OPEN,
    CLOSING,
    CLOSED
}

public class Door
{
    public Vector2 StartPosition { get; set; }
    public Vector2 Position { get; set; }
    public DoorState DoorState { get; set; }
    public DoorRotation DoorRotation { get; set; }
    /// <summary>0-based index into <see cref="MapData.TileTextures"/> for this door's sprite.</summary>
    public int TextureIndex { get; set; }
    public bool RequiresGoldKey { get; set; }
    public bool RequiresSilverKey { get; set; }
    public float TimeDoorHasBeenOpen { get; set; }
    public float TimeDoorHasBeenOpening { get; set; }

    public bool IsLocked => RequiresGoldKey || RequiresSilverKey;
}
