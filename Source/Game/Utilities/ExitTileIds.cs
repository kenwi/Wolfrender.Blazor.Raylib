namespace Game.Utilities;

/// <summary>Level exit wall tiles on <c>spritesheet_tiles.png</c> (handle up / handle down).</summary>
public static class ExitTileIds
{
    public const uint Inactive = 44;
    public const uint Activated = 42;

    /// <summary>Max distance from player to exit tile center (in tile units) to use interact.</summary>
    public const float InteractRadiusTiles = 1.5f;

    /// <summary>Seconds after activation before the level completes.</summary>
    public const float ExitDelaySeconds = 3f;

    public static bool IsExitTile(uint tileId) => tileId == Inactive || tileId == Activated;
}
