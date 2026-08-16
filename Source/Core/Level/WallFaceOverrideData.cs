using System.Text.Json.Serialization;

namespace Game.Core.Level;

/// <summary>JSON DTO for <see cref="WallFaceOverride"/>.</summary>
public class WallFaceOverrideData
{
    public int TileX { get; set; }
    public int TileY { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? North { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? East { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? South { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? West { get; set; }

    public static WallFaceOverrideData FromPlacement(WallFaceOverride placement) => new()
    {
        TileX = placement.TileX,
        TileY = placement.TileY,
        North = WallFaceOverride.IsSet(placement.North) ? placement.North : null,
        East = WallFaceOverride.IsSet(placement.East) ? placement.East : null,
        South = WallFaceOverride.IsSet(placement.South) ? placement.South : null,
        West = WallFaceOverride.IsSet(placement.West) ? placement.West : null
    };

    public WallFaceOverride ToPlacement() => new()
    {
        TileX = TileX,
        TileY = TileY,
        North = WallFaceOverride.IsSet(North) ? North : null,
        East = WallFaceOverride.IsSet(East) ? East : null,
        South = WallFaceOverride.IsSet(South) ? South : null,
        West = WallFaceOverride.IsSet(West) ? West : null
    };
}
