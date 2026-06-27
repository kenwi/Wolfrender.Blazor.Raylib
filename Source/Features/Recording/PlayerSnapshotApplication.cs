using System.Numerics;
using Game.Features.Players;

namespace Game.Features.Recording;

public static class PlayerSnapshotApplication
{
    private const float LookDistance = 1f;

    public static PlayerSnapshot From(Player player)
    {
        Vector3 forward = player.Camera.Target - player.Camera.Position;
        if (forward.LengthSquared() > 0.0001f)
            forward = Vector3.Normalize(forward);
        else
            forward = Vector3.UnitZ;

        return new PlayerSnapshot
        {
            PositionX = player.Position.X,
            PositionY = player.Position.Y,
            PositionZ = player.Position.Z,
            ForwardX = forward.X,
            ForwardY = forward.Y,
            ForwardZ = forward.Z
        };
    }

    public static void ApplyTo(this PlayerSnapshot snapshot, Player player)
    {
        player.Position = new Vector3(snapshot.PositionX, snapshot.PositionY, snapshot.PositionZ);
        player.OldPosition = player.Position;
        player.Velocity = Vector3.Zero;

        var forward = new Vector3(snapshot.ForwardX, snapshot.ForwardY, snapshot.ForwardZ);
        if (forward.LengthSquared() > 0.0001f)
            forward = Vector3.Normalize(forward);
        else
            forward = Vector3.UnitZ;

        var camera = player.Camera;
        camera.Position = player.Position;
        camera.Target = player.Position + forward * LookDistance;
        camera.Up = Vector3.UnitY;
        player.Camera = camera;
    }
}
