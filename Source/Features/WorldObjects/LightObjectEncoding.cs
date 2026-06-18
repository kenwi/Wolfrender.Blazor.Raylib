namespace Game.Features.WorldObjects;

/// <summary>
/// Objects layer palette entry: object ID 3 on <see cref="MapData.Objects"/> is a light fixture
/// (spritesheet cell 3 on <c>Objects.png</c>). Emits point lighting in play mode; does not block movement.
/// Sheet layout and shader limits are owned by the Engine (<see cref="ObjectSprites"/>, <see cref="PrimitiveRenderer"/>).
/// </summary>
public static class LightObjectEncoding
{
    public const uint ObjectId = ObjectSprites.LightObjectId;

    public const int MaxShaderLights = PrimitiveRenderer.MaxShaderLights;

    /// <summary>World-unit radius for fixture falloff (about eight 4-unit tiles).</summary>
    public const float DefaultRadius = PrimitiveRenderer.DefaultTileLightRadius;

    /// <summary>World Y for the light anchor; matches <see cref="Systems.PlacedObjectSystem"/> billboards.</summary>
    public const float WorldAnchorY = 1.5f;

    public static bool IsLightObject(uint objectId) => ObjectSprites.IsLightObject(objectId);
}
