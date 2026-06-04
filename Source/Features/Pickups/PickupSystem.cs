using System.Numerics;
using Game.Features.LevelProgress;
using Game.Features.Combat;
using Raylib_cs;

namespace Game.Features.Pickups;

/// <summary>
/// Spawns, draws, and collects level pickups via tile lookup under the player.
/// </summary>
public class PickupSystem
{
    /// <summary>Nudge tile lookup on world X/Z by half a tile before flooring (see <see cref="LevelData.GetEntityTileFromWorld"/>).</summary>
    private static float CollectTileOffsetZ => LevelData.QuadSize * 0.5f;

    private Texture2D _objectsTexture;
    private readonly ScoreSystem? _scoreSystem;

    private readonly List<Pickup> _activePickups = new();
    private Pickup?[] _pickupByTile = Array.Empty<Pickup?>();
    private int _mapWidth;
    private int _mapHeight;

    public IReadOnlyList<Pickup> ActivePickups => _activePickups;

    public PickupSystem(ScoreSystem? scoreSystem = null) => _scoreSystem = scoreSystem;

    public void SetObjectsTexture(Texture2D texture) => _objectsTexture = texture;

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

        var (tileX, tileY) = LevelData.GetEntityTileFromWorld(
            player.Position.X,
            player.Position.Z);
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

    private void ApplyPickup(Player player, Pickup pickup)
    {
        string positions = FormatPickupPositions(player, pickup);

        if (TreasureScoreCatalog.IsTreasure(pickup.Type))
        {
            int points = TreasureScoreCatalog.GetPoints(pickup.Type);
            _scoreSystem?.OnTreasureCollected(pickup.Type);
            Debug.Log($"Picked up {pickup.Type} (+{points} score, total {_scoreSystem?.LevelScore ?? points}). {positions}");
            return;
        }

        int amount = pickup.Amount;

        switch (pickup.Type)
        {
            case PickupType.Health:
                player.Health = MathF.Min(player.MaxHealth, player.Health + amount);
                Debug.Log($"Picked up health (+{amount}), HP {(int)player.Health}/{(int)player.MaxHealth}. {positions}");
                break;
            case PickupType.Ammo:
                player.Ammo += amount;
                Debug.Log($"Picked up ammo (+{amount}), total {player.Ammo}. {positions}");
                break;
            case PickupType.MachineGun:
                player.Weapons.Grant(WeaponId.MachineGun);
                player.Ammo += amount;
                player.Weapons.TrySetActive(WeaponId.MachineGun);
                Debug.Log($"Picked up machine gun (+{amount} ammo), total {player.Ammo}. {positions}");
                break;
            case PickupType.ChainGun:
                player.Weapons.Grant(WeaponId.ChainGun);
                player.Ammo += amount;
                player.Weapons.TrySetActive(WeaponId.ChainGun);
                Debug.Log($"Picked up chain gun (+{amount} ammo), total {player.Ammo}. {positions}");
                break;
            case PickupType.GoldKey:
                player.HasGoldKey = true;
                Debug.Log($"Picked up gold key. {positions}");
                break;
            case PickupType.SilverKey:
                player.HasSilverKey = true;
                Debug.Log($"Picked up silver key. {positions}");
                break;
        }
    }

    /// <summary>
    /// Spawns an ammo pickup at the tile under <paramref name="worldPosition"/> (e.g. enemy death).
    /// Skips if that tile already has a pickup.
    /// </summary>
    public bool TrySpawnDroppedPickup(PickupType type, Vector3 worldPosition, int amount = 0)
    {
        if (_pickupByTile.Length == 0)
            return false;

        var (tileX, tileY) = LevelData.GetTileFromWorld(worldPosition.X, worldPosition.Z);
        if (tileX < 0 || tileX >= _mapWidth || tileY < 0 || tileY >= _mapHeight)
            return false;

        int idx = TileIndex(tileX, tileY);
        if (_pickupByTile[idx] is not null)
            return false;

        var pickup = new Pickup
        {
            Type = type,
            TileX = tileX,
            TileY = tileY,
            Amount = PickupDefaults.GetAmount(type, amount),
            Position = LevelData.GetTileAnchorWorld(tileX, tileY, 1.5f)
        };

        _pickupByTile[idx] = pickup;
        _activePickups.Add(pickup);
        return true;
    }

    public void Render(Vector3 cameraPosition, Vector3 cameraViewTarget)
    {
        foreach (var pickup in _activePickups)
        {
            if (_objectsTexture.Id > 0)
            {
                PrimitiveRenderer.DrawSpriteTexture(
                    _objectsTexture,
                    pickup.Position,
                    cameraPosition,
                    Color.White,
                    frameRect: PickupSprites.GetFrameRect(pickup.Type),
                    quantizeToEightDirections: false,
                    heightOffset: 0.5f,
                    cameraViewTarget: cameraViewTarget,
                    facingMode: SpriteBillboardGeometry.FacingMode.ViewAligned);
            }
            else
            {
                PrimitiveRenderer.DrawColoredBillboard(
                    pickup.Position,
                    cameraPosition,
                    PickupVisuals.GetColor(pickup.Type),
                    cameraViewTarget: cameraViewTarget,
                    facingMode: SpriteBillboardGeometry.FacingMode.ViewAligned);
            }
        }
    }

    private Pickup CreatePickup(PickupPlacement placement)
    {
        return new Pickup
        {
            Type = placement.Type,
            TileX = placement.TileX,
            TileY = placement.TileY,
            Amount = PickupDefaults.GetAmount(placement.Type, placement.Amount),
            Position = LevelData.GetTileAnchorWorld(placement.TileX, placement.TileY, 1.5f)
        };
    }

    private int TileIndex(int tileX, int tileY) => _mapWidth * tileY + tileX;

    private static string FormatPickupPositions(Player player, Pickup pickup)
    {
        var p = player.Position;
        var m = pickup.Position;
        var (playerTileX, playerTileY) = LevelData.GetTileFromWorld(p.X, p.Z - CollectTileOffsetZ);
        return $"pickup tile ({pickup.TileX}, {pickup.TileY}), player tile ({playerTileX}, {playerTileY}), pickup world ({m.X:F1}, {m.Y:F1}, {m.Z:F1}), player ({p.X:F1}, {p.Y:F1}, {p.Z:F1})";
    }
}
