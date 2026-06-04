using Raylib_cs;

namespace Game.Features.Doors;

/// <summary>Door interaction feedback overlays.</summary>
public static class DoorOverlayHud
{
    public static void DrawLockedHint(DoorSystem doorSystem, int screenWidth, int screenHeight) =>
        Hud.HudBanner.DrawCenter(
            "DOOR LOCKED",
            doorSystem.LockedHintOverlayText,
            doorSystem.LockedHintColor,
            screenWidth,
            screenHeight);
}
