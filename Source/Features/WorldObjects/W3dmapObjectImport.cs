using Game.Core.Level;
using Game.Features.Pickups;

namespace Game.Features.WorldObjects;

/// <summary>
/// Plane-1 static-prop import into <see cref="MapData.Objects"/>.
/// Object IDs are extractor sprite indices: <c>spriteIndex = thing - 21</c>
/// for Wolf statics 23..70. Collectibles stay on <see cref="W3dmapPickupImport"/>.
/// </summary>
public static class W3dmapObjectImport
{
    public const ushort MinStaticThing = 23;
    public const ushort MaxStaticThing = 70;
    public const int ThingToSpriteOffset = 21;

    /// <summary>
    /// Write static props into <paramref name="mapData"/>.Objects.
    /// Overwrites object cells; call on a freshly reset map.
    /// </summary>
    public static void ApplyPlane1(MapData mapData, ReadOnlySpan<ushort> things)
    {
        int side = mapData.Width;
        if (side <= 0 || mapData.Height != side)
            throw new InvalidOperationException("W3DMAP plane 1 requires a square map matching plane 0.");
        if (things.Length != side * side || mapData.Objects.Length != side * side)
            throw new InvalidOperationException("W3DMAP plane 1 size must match plane 0.");

        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                ushort thing = things[y * side + x];
                if (!TryMapThing(thing, out uint objectId))
                    continue;

                mapData.Objects[side * y + x] = objectId;
            }
        }
    }

    /// <summary>
    /// Map a plane-1 thing to an extractor sprite index, or false for
    /// non-statics / collectibles handled by pickup import.
    /// </summary>
    public static bool TryMapThing(ushort thing, out uint objectId)
    {
        objectId = 0;
        if (thing < MinStaticThing || thing > MaxStaticThing)
            return false;
        if (W3dmapPickupImport.TryMapThing(thing, out _, out _))
            return false;

        objectId = (uint)(thing - ThingToSpriteOffset);
        return ObjectSprites.IsValidObjectId(objectId);
    }
}
