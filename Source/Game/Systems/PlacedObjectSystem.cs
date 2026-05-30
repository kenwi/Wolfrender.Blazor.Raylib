using System.Numerics;
using Game.Utilities;
using Raylib_cs;

namespace Game.Systems;

/// <summary>Runtime instance of a blocking object placed on the map grid.</summary>
public sealed class PlacedObject
{
    public int TileX { get; init; }
    public int TileY { get; init; }
    public uint ObjectId { get; init; }
    public Vector3 Position { get; init; }
}

/// <summary>
/// Draws placed blocking objects from <see cref="MapData.Objects"/> as billboards in play mode.
/// </summary>
public class PlacedObjectSystem
{
    private Texture2D _objectsTexture;
    private readonly List<PlacedObject> _objects = new();

    public IReadOnlyList<PlacedObject> Objects => _objects;

    public void SetObjectsTexture(Texture2D texture) => _objectsTexture = texture;

    public void Rebuild(MapData mapData)
    {
        _objects.Clear();

        if (mapData.Objects.Length != mapData.Width * mapData.Height)
            return;

        for (int y = 0; y < mapData.Height; y++)
        {
            for (int x = 0; x < mapData.Width; x++)
            {
                uint objectId = mapData.Objects[mapData.Width * y + x];
                if (!ObjectSprites.IsValidObjectId(objectId))
                    continue;

                _objects.Add(new PlacedObject
                {
                    TileX = x,
                    TileY = y,
                    ObjectId = objectId,
                    Position = LevelData.GetTileAnchorWorld(x, y, 1.5f)
                });
            }
        }
    }

    public void Render(Vector3 cameraPosition)
    {
        if (_objectsTexture.Id == 0)
            return;

        foreach (var obj in _objects)
        {
            PrimitiveRenderer.DrawSpriteTexture(
                _objectsTexture,
                obj.Position,
                cameraPosition,
                Color.White,
                frameRect: ObjectSprites.GetFrameRectForObjectId(obj.ObjectId),
                quantizeToEightDirections: false,
                heightOffset: 0.5f);
        }
    }
}
