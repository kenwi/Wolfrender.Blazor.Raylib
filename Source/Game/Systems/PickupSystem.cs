using System.Numerics;
using Game.Entities;
using Game.Utilities;
using Raylib_cs;

namespace Game.Systems;

/// <summary>
/// Spawns and draws level pickups. Phase A: render only (no collection).
/// </summary>
public class PickupSystem
{
    private readonly List<Pickup> _activePickups = new();
    private Pickup?[] _pickupByTile = Array.Empty<Pickup?>();
    private int _mapWidth;

    public IReadOnlyList<Pickup> ActivePickups => _activePickups;

    public void Rebuild(IReadOnlyList<PickupPlacement> placements, MapData mapData)
    {
        _mapWidth = mapData.Width;
        int tileCount = mapData.Width * mapData.Height;
        _pickupByTile = new Pickup?[tileCount];
        _activePickups.Clear();

        foreach (var placement in placements)
        {
            if (placement.TileX < 0 || placement.TileX >= mapData.Width
                || placement.TileY < 0 || placement.TileY >= mapData.Height)
                continue;

            var pickup = CreatePickup(placement);
            int idx = TileIndex(placement.TileX, placement.TileY);
            _pickupByTile[idx] = pickup;
            _activePickups.Add(pickup);
        }
    }

    public void Render(Vector3 cameraPosition)
    {
        foreach (var pickup in _activePickups)
        {
            PrimitiveRenderer.DrawColoredBillboard(
                pickup.Position,
                cameraPosition,
                PickupVisuals.GetColor(pickup.Type),
                width: 1.5f,
                height: 1.5f);
        }
    }

    private Pickup CreatePickup(PickupPlacement placement)
    {
        float quad = LevelData.QuadSize;
        return new Pickup
        {
            Type = placement.Type,
            TileX = placement.TileX,
            TileY = placement.TileY,
            Amount = PickupDefaults.GetAmount(placement.Type, placement.Amount),
            Position = new Vector3(
                placement.TileX * quad + quad * 0.5f,
                1.5f,
                placement.TileY * quad + quad * 0.5f)
        };
    }

    private int TileIndex(int tileX, int tileY) => _mapWidth * tileY + tileX;
}
