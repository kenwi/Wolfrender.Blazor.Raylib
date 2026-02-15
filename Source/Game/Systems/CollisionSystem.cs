using System.Numerics;
using Game.Entities;
using Game.Utilities;

namespace Game.Systems;

public class CollisionSystem
{
    private readonly LevelData _level;
    private readonly DoorSystem _doorSystem;
    private const float TileSize = 4.0f;

    public CollisionSystem(LevelData level, DoorSystem doorSystem)
    {
        _level = level;
        _doorSystem = doorSystem;
    }

    public bool CheckCollisionAtPosition(Vector3 position, float radius)
    {
        // Check center point
        int tileX = (int)(position.X / TileSize + 0.5f);
        int tileY = (int)(position.Z / TileSize + 0.5f);
        if (_level.GetWallTile(tileX, tileY) > 0)
            return true;

        // Check cardinal directions (N, S, E, W) at collision radius
        // North (positive Z)
        int northTileY = (int)((position.Z + radius) / TileSize + 0.5f);
        if (_level.GetWallTile(tileX, northTileY) > 0)
            return true;

        // South (negative Z)
        int southTileY = (int)((position.Z - radius) / TileSize + 0.5f);
        if (_level.GetWallTile(tileX, southTileY) > 0)
            return true;

        // East (positive X)
        int eastTileX = (int)((position.X + radius) / TileSize + 0.5f);
        if (_level.GetWallTile(eastTileX, tileY) > 0)
            return true;

        // West (negative X)
        int westTileX = (int)((position.X - radius) / TileSize + 0.5f);
        if (_level.GetWallTile(westTileX, tileY) > 0)
            return true;

        // Check diagonal directions at collision radius
        float diagonalOffset = radius * 0.707f; // 1/√2 for 45-degree angle
        
        // Northeast
        int neTileX = (int)((position.X + diagonalOffset) / TileSize + 0.5f);
        int neTileY = (int)((position.Z + diagonalOffset) / TileSize + 0.5f);
        if (_level.GetWallTile(neTileX, neTileY) > 0)
            return true;

        // Northwest
        int nwTileX = (int)((position.X - diagonalOffset) / TileSize + 0.5f);
        int nwTileY = (int)((position.Z + diagonalOffset) / TileSize + 0.5f);
        if (_level.GetWallTile(nwTileX, nwTileY) > 0)
            return true;

        // Southeast
        int seTileX = (int)((position.X + diagonalOffset) / TileSize + 0.5f);
        int seTileY = (int)((position.Z - diagonalOffset) / TileSize + 0.5f);
        if (_level.GetWallTile(seTileX, seTileY) > 0)
            return true;

        // Southwest
        int swTileX = (int)((position.X - diagonalOffset) / TileSize + 0.5f);
        int swTileY = (int)((position.Z - diagonalOffset) / TileSize + 0.5f);
        if (_level.GetWallTile(swTileX, swTileY) > 0)
            return true;

        if (_doorSystem.IsDoorBlocking(position, radius))
            return true;

        return false;
    }

    public void Update(Player player, float deltaTime)
    {
        if (player.Velocity.Length() <= 0)
            return;
            
        // MovementSystem has already set player.Position to the desired position
        // (player.Position = oldPosition + player.Velocity * deltaTime)
        var desiredPosition = player.Position;
        var oldPosition = player.OldPosition;

        // Check if desired position collides using the collision radius
        if (!CheckCollisionAtPosition(desiredPosition, player.CollisionRadius))
        {
            // No collision, safe to move to desired position
            // Position is already set by MovementSystem, so we're done
            return;
        }


        // Collision detected - try sliding
        // First, try sliding on X axis only (keep Z from old position)
        Vector3 slideXPosition = new Vector3(desiredPosition.X, oldPosition.Y, oldPosition.Z);
        if (!CheckCollisionAtPosition(slideXPosition, player.CollisionRadius))
        {
            // X-axis slide works
            player.Position = slideXPosition;
            return;
        }

        // Try sliding on Z axis only (keep X from old position)
        Vector3 slideZPosition = new Vector3(oldPosition.X, oldPosition.Y, desiredPosition.Z);
        if (!CheckCollisionAtPosition(slideZPosition, player.CollisionRadius))
        {
            // Z-axis slide works
            player.Position = slideZPosition;
            return;
        }

        // Both slides failed - stay at old position (completely blocked)
        player.Position = oldPosition;
    }
}

