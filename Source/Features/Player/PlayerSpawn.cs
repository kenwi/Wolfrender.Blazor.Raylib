using System.Numerics;
using Raylib_cs;

namespace Game.Features.Players;

public enum PlayerSpawnApplyMode
{
    /// <summary>Game restart / level load: health, inventory, weapons, camera.</summary>
    FullReset,

    /// <summary>Editor: position and camera from map spawn only.</summary>
    PositionAndCameraOnly,
}

public static class PlayerSpawn
{
    private const float DefaultLookDistance = 1f;
    private const float DefaultFovY = 60f;

    public static void ApplyFromMap(Player player, MapData mapData, PlayerSpawnApplyMode mode = PlayerSpawnApplyMode.FullReset)
    {
        player.Position = LevelData.GetTileAnchorWorld(
            mapData.PlayerSpawnTileX, mapData.PlayerSpawnTileY, mapData.PlayerSpawnWorldY);
        player.OldPosition = player.Position;
        player.Velocity = Vector3.Zero;

        if (mode == PlayerSpawnApplyMode.FullReset)
        {
            player.Health = player.MaxHealth;
            player.WeaponCooldownRemaining = 0f;
            player.ResetInventory();
        }

        ApplyCameraFromMap(player, mapData);
    }

    public static void ApplyCameraFromMap(Player player, MapData mapData)
    {
        float r = mapData.PlayerSpawnRotation;
        var forward = new Vector3(MathF.Cos(r), 0f, MathF.Sin(r));

        var cam = player.Camera;
        cam.Position = player.Position;
        cam.Target = player.Position + forward * DefaultLookDistance;
        cam.Up = new Vector3(0f, 1f, 0f);
        cam.FovY = DefaultFovY;
        cam.Projection = CameraProjection.Perspective;
        player.Camera = cam;
    }
}
