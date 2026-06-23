using System.Numerics;
using Game.Features.Players;

namespace Game.Features.LevelProgress;

/// <summary>
/// Push-wall secrets authored in <see cref="MapData.SecretWalls"/>.
/// Player presses E near an adjacent secret tile to open it (instant slide in phase 3).
/// </summary>
public sealed class SecretSystem
{
    private readonly ScoreSystem _scoreSystem;
    private MapData _mapData = null!;
    private readonly List<RuntimeSecretWall> _secrets = new();

    public SecretSystem(ScoreSystem scoreSystem) => _scoreSystem = scoreSystem;

    public void Rebuild(MapData mapData)
    {
        RestoreAuthoredSecretWalls(mapData);
        _mapData = mapData;
        _secrets.Clear();

        foreach (var placement in mapData.SecretWalls)
        {
            if (placement.TileX < 0 || placement.TileX >= mapData.Width
                || placement.TileY < 0 || placement.TileY >= mapData.Height)
                continue;

            int index = LevelData.GetIndex(placement.TileX, placement.TileY, mapData.Width);
            uint wallTileId = mapData.Walls[index];
            if (wallTileId == 0)
                continue;

            _secrets.Add(new RuntimeSecretWall
            {
                TileX = placement.TileX,
                TileY = placement.TileY,
                Direction = placement.Direction,
                TravelTiles = Math.Max(1, placement.TravelTiles),
                WallTileId = wallTileId
            });
        }
    }

    /// <summary>
    /// Returns true when this frame's interact was consumed (doors should not also open).
    /// </summary>
    public bool Update(float deltaTime, InputState input, Player player)
    {
        _ = deltaTime;
        if (!input.IsInteractPressed || !player.IsAlive)
            return false;

        return TryActivateSecret(player);
    }

    private bool TryActivateSecret(Player player)
    {
        var secret = FindClosestActivatableSecret(player);
        if (secret is null)
            return false;

        ActivateSecret(secret);
        return true;
    }

    private RuntimeSecretWall? FindClosestActivatableSecret(Player player)
    {
        if (_secrets.Count == 0)
            return null;

        float quadSize = LevelData.QuadSize;
        var playerTile = new Vector2(player.Position.X / quadSize, player.Position.Z / quadSize);
        int playerTileX = (int)MathF.Floor(playerTile.X);
        int playerTileY = (int)MathF.Floor(playerTile.Y);

        RuntimeSecretWall? closest = null;
        float closestDistance = float.MaxValue;

        foreach (var secret in _secrets)
        {
            if (secret.IsActivated)
                continue;

            if (!IsAdjacentToPlayerTile(secret.TileX, secret.TileY, playerTileX, playerTileY))
                continue;

            var secretCenter = new Vector2(secret.TileX, secret.TileY);
            float distance = Vector2.Distance(playerTile, secretCenter);
            if (distance > ExitTileIds.InteractRadiusTiles || distance >= closestDistance)
                continue;

            closest = secret;
            closestDistance = distance;
        }

        return closest;
    }

    private static bool IsAdjacentToPlayerTile(int tileX, int tileY, int playerTileX, int playerTileY)
    {
        int dx = Math.Abs(tileX - playerTileX);
        int dy = Math.Abs(tileY - playerTileY);
        return dx <= 1 && dy <= 1 && (dx > 0 || dy > 0);
    }

    private void ActivateSecret(RuntimeSecretWall secret)
    {
        int startIndex = LevelData.GetIndex(secret.TileX, secret.TileY, _mapData.Width);
        uint wallTileId = _mapData.Walls[startIndex];
        if (wallTileId == 0)
            wallTileId = secret.WallTileId;

        _mapData.Walls[startIndex] = 0;

        var (dx, dy) = SecretWallDirectionHelper.ToTileDelta(secret.Direction);
        int endX = secret.TileX + dx * secret.TravelTiles;
        int endY = secret.TileY + dy * secret.TravelTiles;
        if (endX >= 0 && endX < _mapData.Width && endY >= 0 && endY < _mapData.Height)
        {
            int endIndex = LevelData.GetIndex(endX, endY, _mapData.Width);
            _mapData.Walls[endIndex] = wallTileId;
        }

        secret.IsActivated = true;
        _scoreSystem.OnSecretFound();

        Debug.Log(
            $"Secret wall opened at tile ({secret.TileX}, {secret.TileY}) " +
            $"-> ({endX}, {endY}), travel={secret.TravelTiles} {secret.Direction}.");
    }

    /// <summary>
    /// Puts moved secret walls back on their authored tile before a rebuild (level restart, reload, re-enter play).
    /// </summary>
    private static void RestoreAuthoredSecretWalls(MapData mapData)
    {
        foreach (var placement in mapData.SecretWalls)
        {
            if (placement.TileX < 0 || placement.TileX >= mapData.Width
                || placement.TileY < 0 || placement.TileY >= mapData.Height)
                continue;

            int travel = Math.Max(1, placement.TravelTiles);
            var (dx, dy) = SecretWallDirectionHelper.ToTileDelta(placement.Direction);
            int startIndex = LevelData.GetIndex(placement.TileX, placement.TileY, mapData.Width);
            uint wallTileId = mapData.Walls[startIndex];

            int endX = placement.TileX + dx * travel;
            int endY = placement.TileY + dy * travel;
            if (wallTileId == 0
                && endX >= 0 && endX < mapData.Width
                && endY >= 0 && endY < mapData.Height)
            {
                int endIndex = LevelData.GetIndex(endX, endY, mapData.Width);
                wallTileId = mapData.Walls[endIndex];
                if (wallTileId > 0)
                    mapData.Walls[endIndex] = 0;
            }

            if (wallTileId > 0)
                mapData.Walls[startIndex] = wallTileId;
        }
    }

    private sealed class RuntimeSecretWall
    {
        public int TileX { get; init; }
        public int TileY { get; init; }
        public SecretWallDirection Direction { get; init; }
        public int TravelTiles { get; init; }
        public uint WallTileId { get; init; }
        public bool IsActivated { get; set; }
    }
}
