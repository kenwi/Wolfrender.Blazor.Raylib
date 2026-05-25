using System.Numerics;
using Game.Entities;
using Game.Utilities;
using Raylib_cs;

namespace Game.Systems;

/// <summary>
/// Spawns, draws, and collects level pickups via tile lookup under the player.
/// </summary>
public class PickupSystem
{
    private readonly List<Pickup> _activePickups = new();
    private Pickup?[] _pickupByTile = Array.Empty<Pickup?>();
    private int _mapWidth;
    private int _mapHeight;

    public IReadOnlyList<Pickup> ActivePickups => _activePickups;

    public void Rebuild(IReadOnlyList<PickupPlacement> placements, MapData mapData)
    {
        _mapWidth = mapData.Width;
        _mapHeight = mapData.Height;
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

    public void Update(Player player)
    {
        if (!player.IsAlive || _pickupByTile.Length == 0)
            return;

        int tileX = (int)(player.Position.X / LevelData.QuadSize + 0.5f);
        int tileY = (int)(player.Position.Z / LevelData.QuadSize + 0.5f);
        if (tileX < 0 || tileX >= _mapWidth || tileY < 0 || tileY >= _mapHeight)
            return;

        int idx = TileIndex(tileX, tileY);
        var pickup = _pickupByTile[idx];
        if (pickup is null)
            return;

        ApplyPickup(player, pickup);
        _pickupByTile[idx] = null;
        _activePickups.Remove(pickup);
    }

    public void Render(Vector3 cameraPosition)
    {
        foreach (var pickup in _activePickups)
        {
            PrimitiveRenderer.DrawColoredBillboard(
                pickup.Position,
                cameraPosition,
                PickupVisuals.GetColor(pickup.Type));
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

    private static void ApplyPickup(Player player, Pickup pickup)
    {
        int amount = pickup.Amount;
        switch (pickup.Type)
        {
            case PickupType.Health:
                player.Health = MathF.Min(player.MaxHealth, player.Health + amount);
                Debug.Log($"Picked up health (+{amount}), HP {(int)player.Health}/{(int)player.MaxHealth}");
                break;
            case PickupType.Ammo:
                player.Ammo += amount;
                Debug.Log($"Picked up ammo (+{amount}), total {player.Ammo}");
                break;
            case PickupType.MachineGun:
                player.HasMachineGun = true;
                player.Ammo += amount;
                Debug.Log($"Picked up machine gun (+{amount} ammo), total {player.Ammo}");
                break;
            case PickupType.GoldKey:
                player.HasGoldKey = true;
                Debug.Log("Picked up gold key");
                break;
            case PickupType.SilverKey:
                player.HasSilverKey = true;
                Debug.Log("Picked up silver key");
                break;
        }
    }
}
