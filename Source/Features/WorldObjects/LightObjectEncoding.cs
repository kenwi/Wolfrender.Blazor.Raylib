namespace Game.Features.WorldObjects;

/// <summary>
/// Objects layer light-fixture semantics for <see cref="MapData.Objects"/>.
/// IDs and blocking rules live on <see cref="ObjectSprites"/>; shader limits stay in Engine.
/// </summary>
public static class LightObjectEncoding
{
    public static readonly uint[] ObjectIds = ObjectSprites.LightObjectIds;

    public const int MaxShaderLights = PrimitiveRenderer.MaxShaderLights;

    /// <summary>World-unit radius for fixture falloff (about eight 4-unit tiles).</summary>
    public const float DefaultRadius = PrimitiveRenderer.DefaultTileLightRadius;

    /// <summary>World Y for the light anchor; matches <see cref="PlacedObjectSystem"/> billboards.</summary>
    public const float WorldAnchorY = 1.5f;

    public static bool IsLightObject(uint objectId) => ObjectSprites.IsLightObject(objectId);
}
