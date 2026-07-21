using System.Numerics;
using Game.Engine.Rendering;
using Raylib_cs;

namespace Game.Features.Doors;

public class DoorSystem : IMovementBlocker, IDoorPortalState
{
    private readonly List<Texture2D> _textures;
    private readonly List<Door> _doors;
    private readonly int _quadSize;
    private Vector2 _playerPosition;
    private IDoorOccupancyProbe _occupancy = EmptyDoorOccupancyProbe.Instance;

    private const float LockedHintDurationSeconds = 2.5f;
    private float _lockedHintRemaining;
    private string _lockedHintOverlayText = string.Empty;
    private Color _lockedHintColor = Color.RayWhite;

    public List<Door> Doors => _doors;
    public bool HasLockedHint => _lockedHintRemaining > 0f;
    public string LockedHintOverlayText => _lockedHintOverlayText;
    public Color LockedHintColor => _lockedHintColor;

    public DoorSystem(uint[] doorTiles, int mapWidth, List<Texture2D> textures)
    {
        _textures = textures;
        _doors = new List<Door>(20);
        _quadSize = LevelData.QuadSize;

        Rebuild(doorTiles, mapWidth);
    }

    /// <summary>
    /// Rebuild the door list from the current tile data.
    /// Call this when the level data has changed (e.g. after editing in the level editor).
    /// </summary>
    public void Rebuild(uint[] doorTiles, int mapWidth)
    {
        _doors.Clear();

        for (int index = 0; index < doorTiles.Length; index++)
        {
            var value = doorTiles[index];
            if (!DoorTileEncoding.TryParse(value, out var doorInfo))
                continue;

            var colRow = LevelData.GetColRow(index, mapWidth);
            _doors.Add(new Door
            {
                Position = new Vector2(colRow.col, colRow.row),
                StartPosition = new Vector2(colRow.col, colRow.row),
                DoorRotation = doorInfo.Rotation,
                TextureIndex = doorInfo.TextureIndex,
                RequiresGoldKey = doorInfo.LockKind == DoorLockKind.Gold,
                RequiresSilverKey = doorInfo.LockKind == DoorLockKind.Silver,
                DoorState = DoorState.CLOSED
            });
        }
    }

    public void Render()
    {
        foreach(var door in _doors)
        {
            if (door.DoorRotation == DoorRotation.HORIZONTAL)
            {
                PrimitiveRenderer.DrawDoorTextureH(_textures[door.TextureIndex], new Vector3(door.Position.X * _quadSize, 2, (door.Position.Y - 1) * _quadSize), _quadSize, _quadSize, _quadSize, Raylib_cs.Color.White);
            }
            else
            {
                PrimitiveRenderer.DrawDoorTextureV(_textures[door.TextureIndex], new Vector3(door.Position.X * _quadSize, 2, door.Position.Y * _quadSize), _quadSize, _quadSize, _quadSize, Raylib_cs.Color.White);
            }
        }
    }

    public void Update(
        float deltaTime,
        InputState input,
        Vector3 actorWorldPosition,
        IDoorKeyInventory keys,
        IDoorOccupancyProbe occupancy)
    {
        _playerPosition = new Vector2(actorWorldPosition.X / _quadSize, actorWorldPosition.Z / _quadSize);
        _occupancy = occupancy ?? EmptyDoorOccupancyProbe.Instance;

        if (_lockedHintRemaining > 0f)
            _lockedHintRemaining = MathF.Max(0f, _lockedHintRemaining - deltaTime);

        if (input.IsInteractPressed)
            TryInteractOpenDoor(keys);

        Animate(deltaTime);
    }

    private void TryInteractOpenDoor(IDoorKeyInventory keys)
    {
        var closestDoor = FindClosestDoor(_playerPosition);
        if (closestDoor == null)
            return;

        var doorCenter = closestDoor.Position + new Vector2(0.5f, 0.5f);
        if (Vector2.Distance(_playerPosition, doorCenter) >= 1.5f)
            return;

        if (!CanPlayerOpen(closestDoor, keys))
        {
            ShowLockedHint(closestDoor);
            return;
        }

        OpenDoor(closestDoor);
    }

    public static bool CanPlayerOpen(Door door, IDoorKeyInventory keys)
    {
        if (door.RequiresGoldKey && !keys.HasGoldKey)
            return false;
        if (door.RequiresSilverKey && !keys.HasSilverKey)
            return false;
        return true;
    }

    public static string GetLockedMessage(Door door)
    {
        if (door.RequiresGoldKey)
            return "Door is locked (gold key required)";
        if (door.RequiresSilverKey)
            return "Door is locked (silver key required)";
        return "Door is locked";
    }

    public static string GetLockedOverlayText(Door door)
    {
        if (door.RequiresGoldKey)
            return "GOLD KEY REQUIRED";
        if (door.RequiresSilverKey)
            return "SILVER KEY REQUIRED";
        return "KEY REQUIRED";
    }

    public static Color GetLockedOverlayColor(Door door)
    {
        if (door.RequiresGoldKey)
            return new Color(255, 210, 40, 255);
        if (door.RequiresSilverKey)
            return new Color(200, 220, 255, 255);
        return Color.RayWhite;
    }

    private void ShowLockedHint(Door door)
    {
        _lockedHintOverlayText = GetLockedOverlayText(door);
        _lockedHintColor = GetLockedOverlayColor(door);
        _lockedHintRemaining = LockedHintDurationSeconds;
        Debug.Log(GetLockedMessage(door));
    }

    public Door? FindClosestDoor(Vector2 position)
    {
        var closestDoor = _doors.OrderBy(d => Vector2.Distance(d.Position, position)).FirstOrDefault();
        return closestDoor;
    }

    public void OpenDoor(Door door)
    {
        if (door.DoorState is DoorState.OPEN or DoorState.OPENING)
            return;

        door.DoorState = DoorState.OPENING;
        door.TimeDoorHasBeenOpen = 0;
        door.TimeDoorHasBeenOpening = 0;
    }

    /// <summary><see cref="IMovementBlocker"/>: closed/moving doors block movement.</summary>
    public bool IsBlocking(Vector3 position, float radius) => IsDoorBlocking(position, radius);

    public bool IsDoorBlocking(Vector3 playerPosition, float radius)
    {
        var position = new Vector2(playerPosition.X / _quadSize, playerPosition.Z / _quadSize);
        var closestDoor = FindClosestDoor(position);
        if (closestDoor != null)
        {
            float distanceFromPlayer;
            radius /= 4;
            if (closestDoor.DoorRotation == DoorRotation.HORIZONTAL)
            {
                distanceFromPlayer = Math.Abs(position.Y - closestDoor.Position.Y);
                if (distanceFromPlayer < radius && closestDoor.DoorState != DoorState.OPEN)
                {
                    if (position.X + 0.5 < closestDoor.Position.X
                        || position.X - 0.5 > closestDoor.Position.X)
                    {
                        return false;
                    }
                    return true;

                }
            }

            if (closestDoor.DoorRotation == DoorRotation.VERTICAL)
            {
                distanceFromPlayer = Math.Abs(position.X - closestDoor.Position.X);
                if (distanceFromPlayer < radius && closestDoor.DoorState != DoorState.OPEN)
                {
                    if (position.Y + 0.5 < closestDoor.Position.Y
                        || position.Y - 0.5 > closestDoor.Position.Y)
                    {
                        return false;
                    }
                    return true;
                }
            }
        }
        return false;
    }

    private bool IsOccupiedAtTile(Vector2 tilePosition) =>
        _occupancy.IsTileOccupied((int)tilePosition.X, (int)tilePosition.Y);

    public void Animate(float deltaTime)
    {
        foreach(var door in _doors)
        {
            var distanceDoorHasTraveled = Vector2.Distance(door.StartPosition, door.Position);
            switch (door.DoorState)
            {
                case DoorState.CLOSED:
                    break;
                case DoorState.OPEN:
                    door.TimeDoorHasBeenOpen += deltaTime;
                    if (door.TimeDoorHasBeenOpen > 1f)
                    {
                        door.DoorState = DoorState.CLOSING;
                    }
                    break;
                case DoorState.OPENING:
                    door.TimeDoorHasBeenOpening += deltaTime;
                    if (distanceDoorHasTraveled > 1.0f)
                    {
                        door.DoorState = DoorState.OPEN;
                        break;
                    }

                    if (door.DoorRotation == DoorRotation.HORIZONTAL)
                    {
                        door.Position += new Vector2(1, 0) * deltaTime;
                    }
                    else
                    {
                        door.Position += new Vector2(0, 1) * deltaTime;
                    }
                    break;
                case DoorState.CLOSING:
                    // Avoid closing door on player or other actors
                    var idoorPosition = new Vector2((int)door.StartPosition.X, (int)door.StartPosition.Y);

                    if (IsOccupiedAtTile(idoorPosition))
                        break;

                    if (distanceDoorHasTraveled < 0.01f)
                    {
                        door.DoorState = DoorState.CLOSED;
                        door.Position = door.StartPosition;
                        door.TimeDoorHasBeenOpen = 0;
                        door.TimeDoorHasBeenOpening = 0;
                        break;
                    }

                    if (door.DoorRotation == DoorRotation.HORIZONTAL)
                    {
                        door.Position -= new Vector2(1, 0) * deltaTime;
                    }
                    else
                    {
                        door.Position -= new Vector2(0, 1) * deltaTime;
                    }
                    break;
                default:
                    break;
            }
        }
    }

    public bool IsClosedAt(int tileX, int tileY)
    {
        foreach (var door in _doors)
        {
            int doorTileX = (int)MathF.Round(door.StartPosition.X);
            int doorTileY = (int)MathF.Round(door.StartPosition.Y);
            if (doorTileX != tileX || doorTileY != tileY)
                continue;

            return door.DoorState == DoorState.CLOSED;
        }

        return true;
    }

    public bool IsPassableAt(int tileX, int tileY)
    {
        foreach (var door in _doors)
        {
            int doorTileX = (int)MathF.Round(door.StartPosition.X);
            int doorTileY = (int)MathF.Round(door.StartPosition.Y);
            if (doorTileX != tileX || doorTileY != tileY)
                continue;

            return door.DoorState != DoorState.CLOSED;
        }

        return false;
    }
}

file sealed class EmptyDoorOccupancyProbe : IDoorOccupancyProbe
{
    public static readonly EmptyDoorOccupancyProbe Instance = new();
    public bool IsTileOccupied(int tileX, int tileY) => false;
}
