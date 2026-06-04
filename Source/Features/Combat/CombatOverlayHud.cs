using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Combat;

/// <summary>Weapon, ammo, and combat feedback overlays.</summary>
public static class CombatOverlayHud
{
    public static void DrawInventory(Player player)
    {
        const int fontSize = 18;
        int y = 68;

        var active = WeaponCatalog.Get(player.Weapons.ActiveWeapon);
        DrawText($"WEAPON: {active.DisplayName}", 10, y, fontSize, Color.RayWhite);
        y += 24;

        DrawText($"AMMO: {player.Ammo}", 10, y, fontSize, new Color(255, 220, 40, 255));
        y += 24;

        var goldColor = player.HasGoldKey ? new Color(255, 210, 40, 255) : new Color(100, 90, 50, 255);
        var silverColor = player.HasSilverKey ? new Color(200, 220, 255, 255) : new Color(90, 95, 110, 255);
        DrawText("KEYS:", 10, y, fontSize, Color.RayWhite);
        DrawText(" GOLD", 58, y, fontSize, goldColor);
        DrawText(" SILVER", 118, y, fontSize, silverColor);

        y += 24;
        DrawText("1 KNIFE  2 PISTOL  3 MG  4 CG", 10, y, 14, new Color(180, 180, 180, 255));
    }

    public static void DrawNoAmmoHint(WeaponSystem weaponSystem, int screenWidth, int screenHeight) =>
        Hud.HudBanner.DrawCenter(
            weaponSystem.NoAmmoHintSubtitle,
            weaponSystem.NoAmmoHintTitle,
            weaponSystem.NoAmmoHintColor,
            screenWidth,
            screenHeight);
}
