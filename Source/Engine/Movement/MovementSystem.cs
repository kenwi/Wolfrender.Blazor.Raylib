using System.Numerics;
using Game.Features.Players;

namespace Game.Engine.Movement;

public class MovementSystem
{
    public void Update(Player player, float deltaTime)
    {
        player.OldPosition = player.Position;
        // Calculate desired position from velocity
        // CollisionSystem will then resolve collisions before final position update
        var desiredPosition = player.Position + player.Velocity * deltaTime;
        player.Position = desiredPosition;
    }
}

