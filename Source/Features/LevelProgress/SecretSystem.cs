using System.Numerics;
using Game.Engine.Movement;
using Game.Engine.Rendering;
using Game.Features.Players;
using Raylib_cs;

namespace Game.Features.LevelProgress;

/// <summary>
/// Push-wall secrets authored in <see cref="MapData.SecretWalls"/>.
/// Player presses E near an adjacent secret tile to slide the wall aside.
/// </summary>
public sealed class SecretSystem : IMovementBlocker
{
    private const float SlideSpeedTilesPerSecond = 1f;

    private readonly ScoreSystem _scoreSystem;
    private readonly List<Texture2D> _textures;
    private MapData _mapData = null!;
    private readonly List<SecretWall> _secrets = new();
    private readonly int _quadSize;

    public IReadOnlyList<SecretWall> Secrets => _secrets;

    public SecretSystem(ScoreSystem scoreSystem, List<Texture2D> textures)
    {
        _scoreSystem = scoreSystem;
        _textures = textures;
        _quadSize = LevelData.QuadSize;
    }

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

            var start = new Vector2(placement.TileX, placement.TileY);
            _secrets.Add(new SecretWall
            {
                TileX = placement.TileX,
                TileY = placement.TileY,
                StartPosition = start,
                Position = start,
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
        Animate(deltaTime);

        if (!input.IsInteractPressed || !player.IsAlive)
            return false;

        return TryActivateSecret(player);
    }

    public void Render(Vector3 playerPosition)
    {
        foreach (var secret in _secrets)
        {
            if (secret.State != SecretWallState.Sliding)
                continue;

            int texIndex = (int)secret.WallTileId - 1;
            if (texIndex < 0 || texIndex >= _textures.Count)
                continue;

            var worldPos = new Vector3(
                secret.Position.X * _quadSize,
                2,
                secret.Position.Y * _quadSize);
            PrimitiveRenderer.DrawCubeTexture(
                _textures[texIndex],
                worldPos,
                _quadSize,
                _quadSize,
                _quadSize,
                Color.White,
                playerPosition);
        }
    }

    public bool IsBlocking(Vector3 position, float radius)
    {
        var (entityTileX, entityTileY) = LevelData.GetEntityTileFromWorld(position.X, position.Z);

        foreach (var secret in _secrets)
        {
            if (secret.State != SecretWallState.Sliding)
                continue;

            var occupied = GetOccupiedTile(secret.Position);
            if (occupied.x == entityTileX && occupied.y == entityTileY)
                return true;
        }

        return false;
    }

    private void Animate(float deltaTime)
    {
        foreach (var secret in _secrets)
        {
            if (secret.State != SecretWallState.Sliding)
                continue;

            var (dx, dy) = SecretWallDirectionHelper.ToTileDelta(secret.Direction);
            var step = new Vector2(dx, dy) * (SlideSpeedTilesPerSecond * deltaTime);
            var nextPosition = secret.Position + step;
            var nextOccupied = GetOccupiedTile(nextPosition);

            if (!CanOccupyTile(nextOccupied.x, nextOccupied.y, secret))
            {
                CompleteSlide(secret, secret.Position);
                continue;
            }

            secret.Position = nextPosition;
            float traveled = Vector2.Distance(secret.StartPosition, secret.Position);
            if (traveled + 0.001f >= secret.TravelTiles)
                CompleteSlide(secret, secret.EndPosition);
        }
    }

    private bool TryActivateSecret(Player player)
    {
        var secret = FindClosestActivatableSecret(player);
        if (secret is null)
            return false;

        BeginSlide(secret);
        return true;
    }

    private SecretWall? FindClosestActivatableSecret(Player player)
    {
        if (_secrets.Count == 0)
            return null;

        float quadSize = LevelData.QuadSize;
        var playerTile = new Vector2(player.Position.X / quadSize, player.Position.Z / quadSize);
        int playerTileX = (int)MathF.Floor(playerTile.X);
        int playerTileY = (int)MathF.Floor(playerTile.Y);

        SecretWall? closest = null;
        float closestDistance = float.MaxValue;

        foreach (var secret in _secrets)
        {
            if (secret.State != SecretWallState.Idle)
                continue;

            if (!IsAdjacentToPlayerTile(secret.TileX, secret.TileY, playerTileX, playerTileY))
                continue;

            var secretCenter = LevelData.GetTileCenterTileSpace(secret.TileX, secret.TileY);
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

    private void BeginSlide(SecretWall secret)
    {
        secret.State = SecretWallState.Sliding;
        secret.StartPosition = new Vector2(secret.TileX, secret.TileY);
        secret.Position = secret.StartPosition;

        int startIndex = LevelData.GetIndex(secret.TileX, secret.TileY, _mapData.Width);
        _mapData.Walls[startIndex] = 0;

        if (!secret.HasScored)
        {
            secret.HasScored = true;
            _scoreSystem.OnSecretFound();
        }

        Debug.Log(
            $"Secret wall sliding from ({secret.TileX}, {secret.TileY}) " +
            $"travel={secret.TravelTiles} {secret.Direction}.");
    }

    private void CompleteSlide(SecretWall secret, Vector2 finalPosition)
    {
        secret.Position = finalPosition;
        secret.State = SecretWallState.Open;

        var occupied = GetOccupiedTile(finalPosition);
        if (occupied.x >= 0 && occupied.x < _mapData.Width
            && occupied.y >= 0 && occupied.y < _mapData.Height)
        {
            int endIndex = LevelData.GetIndex(occupied.x, occupied.y, _mapData.Width);
            _mapData.Walls[endIndex] = secret.WallTileId;
        }

        Debug.Log(
            $"Secret wall opened at tile ({occupied.x}, {occupied.y}).");
    }

    private bool CanOccupyTile(int tileX, int tileY, SecretWall secret)
    {
        if (tileX < 0 || tileX >= _mapData.Width || tileY < 0 || tileY >= _mapData.Height)
            return false;

        int index = LevelData.GetIndex(tileX, tileY, _mapData.Width);
        uint wall = _mapData.Walls[index];
        if (wall == 0)
            return true;

        var end = GetOccupiedTile(secret.EndPosition);
        return tileX == end.x && tileY == end.y && wall == secret.WallTileId;
    }

    private static (int x, int y) GetOccupiedTile(Vector2 position) =>
        ((int)MathF.Floor(position.X + 0.5f), (int)MathF.Floor(position.Y + 0.5f));

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
}
