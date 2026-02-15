using System.Numerics;
using Game.Entities;
using Game.Utilities;
using Raylib_cs;

namespace Game.Systems;

public class DoorSystem
{
    private readonly List<Texture2D> _textures;
    private readonly List<Door> _doors;
    private readonly int _quadSize;
    private Vector2 _playerPosition;
    private IReadOnlyList<Enemy> _enemies = Array.Empty<Enemy>();

    public List<Door> Doors => _doors;

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
            if (value > 0)
            {
                var colRow = LevelData.GetColRow(index, mapWidth);
                var door = new Door()
                {
                    Position = new Vector2(colRow.col, colRow.row),
                    StartPosition = new Vector2(colRow.col, colRow.row),
                    DoorRotation = (DoorRotation)value,
                    DoorState = DoorState.CLOSED
                };
                _doors.Add(door);
            }
        }
    }

    public void Render()
    {
        foreach(var door in _doors)
        {
            if (door.DoorRotation == DoorRotation.HORIZONTAL)
            {
                PrimitiveRenderer.DrawDoorTextureH(_textures[6], new Vector3(door.Position.X * _quadSize, 2, (door.Position.Y - 1) * _quadSize), _quadSize, _quadSize, _quadSize, Raylib_cs.Color.White);
            }
            else
            {
                PrimitiveRenderer.DrawDoorTextureV(_textures[6], new Vector3(door.Position.X * _quadSize, 2, door.Position.Y * _quadSize), _quadSize, _quadSize, _quadSize, Raylib_cs.Color.White);
            }
        }
    }
    
    public void Update(float deltaTime, InputState input, Vector3 playerPosition, IReadOnlyList<Enemy> enemies)
    {
        _playerPosition = new Vector2(playerPosition.X / _quadSize, playerPosition.Z / _quadSize);
        _enemies = enemies;
        if (input.IsInteractPressed)
        {
            var closestDoor = FindClosestDoor(_playerPosition);
            if (closestDoor != null)
            {
                var distanceFromPlayer = Vector2.Distance(_playerPosition, closestDoor.Position);
                if (closestDoor != null && distanceFromPlayer < 1.5f)
                {
                    OpenDoor(closestDoor);
                }
            }
        }

        Animate(deltaTime);
    }

    public Door? FindClosestDoor(Vector2 position)
    {
        var closestDoor = _doors.OrderBy(d => Vector2.Distance(d.Position, position)).FirstOrDefault();
        return closestDoor;
    }

    public void OpenDoor(Door door)
    {
        door.DoorState = DoorState.OPENING;
        door.TimeDoorHasBeenOpen = 0;
        door.TimeDoorHasBeenOpening = 0;
    }

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

            // if (closestDoor.TimeDoorHasBeenOpening > 0.5)
            // {
            //     // TODO: handle door auto-close logic
            // }
        }
        return false;
    }

    private bool IsPlayerOnTile(Vector2 tilePosition)
    {
        var iPlayerPosition = new Vector2((int)(_playerPosition.X + 0.5f), (int)(_playerPosition.Y + 0.5f));
        return iPlayerPosition == tilePosition;
    }

    private bool IsEnemyOnTile(Vector2 tilePosition)
    {
        foreach (var enemy in _enemies)
        {
            var enemyTile = new Vector2(
                (int)(enemy.Position.X / _quadSize + 0.5f),
                (int)(enemy.Position.Z / _quadSize + 0.5f));
            if (enemyTile == tilePosition)
                return true;
        }
        return false;
    }

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
                    // Avoid closing door on player or enemies
                    var idoorPosition = new Vector2((int)door.StartPosition.X, (int)door.StartPosition.Y);
                    
                    if (IsPlayerOnTile(idoorPosition))
                        break;

                    if (IsEnemyOnTile(idoorPosition))
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
}

