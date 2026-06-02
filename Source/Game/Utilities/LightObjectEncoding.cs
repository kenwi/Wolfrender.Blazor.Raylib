namespace Game.Utilities;

/// <summary>
/// Objects layer palette entry: object ID 3 on <see cref="MapData.Objects"/> is a light fixture
/// (spritesheet cell 3 on <c>Objects.png</c>). Emits point lighting in play mode; does not block movement.
/// </summary>
public static class LightObjectEncoding
{
    public const uint ObjectId = 3;

    public const int MaxShaderLights = 8;

    /// <summary>World-unit radius for fixture falloff (about seven 4-unit tiles).</summary>
    public const float DefaultRadius = 28f;

    /// <summary>World Y for the light anchor; matches <see cref="Systems.PlacedObjectSystem"/> billboards.</summary>
    public const float WorldAnchorY = 1.5f;

    public static bool IsLightObject(uint objectId) => objectId == ObjectId;
}
