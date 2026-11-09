using Game.Core.Level;

namespace Game.Features.Pickups;

/// <summary>
/// Decodes Wolfenstein 3D plane-1 static collectibles into
/// <see cref="PickupPlacement"/> records. Thing numbers match ScanInfoPlane()
/// in WL_GAME.C (range 23..70); decorations are ignored.
/// </summary>
public static class W3dmapPickupImport
{
    // Amount 0 → PickupDefaults at runtime. Food/dog food use Wolf-like smaller heals.
    private static readonly Dictionary<ushort, (PickupType Type, int Amount)> Collectibles = new()
    {
        { 29, (PickupType.Health, 4) },          // dog food
        { 43, (PickupType.GoldKey, 0) },
        { 44, (PickupType.SilverKey, 0) },
        { 47, (PickupType.Health, 10) },         // food
        { 48, (PickupType.Health, 0) },          // first aid
        { 49, (PickupType.Ammo, 0) },
        { 50, (PickupType.MachineGun, 0) },
        { 51, (PickupType.ChainGun, 0) },
        { 52, (PickupType.TreasureCross, 0) },
        { 53, (PickupType.TreasureChalice, 0) },
        { 54, (PickupType.TreasureChest, 0) },
        { 55, (PickupType.TreasureCrown, 0) },
        // 56 extra_life_orb: no PickupType yet; skipped.
    };

    /// <summary>
    /// Append plane-1 collectibles onto <paramref name="mapData"/>.Pickups.
    /// Does not clear existing pickups; call after a fresh map reset.
    /// </summary>
    public static void ApplyPlane1(MapData mapData, ReadOnlySpan<ushort> things)
    {
        int side = mapData.Width;
        if (side <= 0 || mapData.Height != side)
            throw new InvalidOperationException("W3DMAP plane 1 requires a square map matching plane 0.");
        if (things.Length != side * side)
            throw new InvalidOperationException("W3DMAP plane 1 size must match plane 0.");

        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                ushort thing = things[y * side + x];
                if (!Collectibles.TryGetValue(thing, out var collectible))
                    continue;

                mapData.Pickups.Add(new PickupPlacement
                {
                    TileX = x,
                    TileY = y,
                    Type = collectible.Type,
                    Amount = collectible.Amount
                });
            }
        }
    }

    public static bool TryMapThing(ushort thing, out PickupType type, out int amount)
    {
        if (Collectibles.TryGetValue(thing, out var collectible))
        {
            type = collectible.Type;
            amount = collectible.Amount;
            return true;
        }

        type = default;
        amount = 0;
        return false;
    }
}
