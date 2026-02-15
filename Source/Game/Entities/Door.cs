using System.Numerics;

namespace Game.Entities;

public enum DoorRotation
{
    HORIZONTAL = 7, // corresponds with texture id
    VERTICAL = 8
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
    public float TimeDoorHasBeenOpen { get; set; }
    public float TimeDoorHasBeenOpening { get; set; }
}
