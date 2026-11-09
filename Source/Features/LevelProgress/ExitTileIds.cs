using Game.Engine.Rendering;

namespace Game.Features.LevelProgress;

/// <summary>Level exit wall tiles: the down/up elevator switch textures.</summary>
public static class ExitTileIds
{
    public static readonly uint Inactive = RequireId("elevator_switch_down");
    public static readonly uint Activated = RequireId("elevator_switch_up");

    /// <summary>Max distance from player to exit tile center (in tile units) to use interact.</summary>
    public const float InteractRadiusTiles = 1.5f;

    /// <summary>Seconds after activation before the level completes.</summary>
    public const float ExitDelaySeconds = 3f;

    public static bool IsExitTile(uint tileId) => tileId == Inactive || tileId == Activated;

    private static uint RequireId(string textureName)
    {
        uint id = WallTilePalette.GetIdByName(textureName);
        if (id == 0)
            throw new InvalidOperationException($"Exit texture '{textureName}' is missing from the wall palette.");
        return id;
    }
}
