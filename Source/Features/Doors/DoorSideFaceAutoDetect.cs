using Game.Core.Level;
using Game.Engine.Rendering;

namespace Game.Features.Doors;

/// <summary>
/// When a door is framed by walls, paints the inward faces of those walls with
/// the door-side texture (<c>100_door_side.png</c>).
/// Horizontal doors use east/west neighbors; vertical doors use north/south.
/// </summary>
public static class DoorSideFaceAutoDetect
{
    public const string DoorSideFileName = "100_door_side.png";

    public readonly record struct FaceAssignment(int TileX, int TileY, WallFace Face, uint TileId);

    public static uint DoorSideTileId => WallTilePalette.GetIdByFileName(DoorSideFileName);

    /// <summary>Apply door-side faces for every door tile on the map.</summary>
    public static void ApplyAllDoors(MapData mapData)
    {
        for (int y = 0; y < mapData.Height; y++)
        {
            for (int x = 0; x < mapData.Width; x++)
            {
                if (DoorTileEncoding.IsDoorTile(mapData.GetTile(mapData.Doors, x, y)))
                    ApplyForDoor(mapData, x, y);
            }
        }
    }

    /// <summary>Apply door-side faces for one door cell, if it is a door tile.</summary>
    public static void ApplyForDoor(MapData mapData, int doorX, int doorY) =>
        ApplyAssignments(mapData, PlanForDoor(mapData, doorX, doorY));

    /// <summary>Write planned face assignments onto <paramref name="mapData"/>.</summary>
    public static void ApplyAssignments(MapData mapData, IEnumerable<FaceAssignment> assignments)
    {
        foreach (var assignment in assignments)
            ApplyAssignment(mapData, assignment);
    }

    /// <summary>
    /// Planned face overrides for a door: horizontal → west face on X+1 and
    /// east face on X-1; vertical → north face on Y+1 and south face on Y-1.
    /// Only emits assignments when the neighbor cell has a wall.
    /// </summary>
    public static IEnumerable<FaceAssignment> PlanForDoor(MapData mapData, int doorX, int doorY)
    {
        if (!DoorTileEncoding.IsDoorTile(mapData.GetTile(mapData.Doors, doorX, doorY)))
            yield break;

        uint sideId = DoorSideTileId;
        if (sideId == 0)
            yield break;

        var rotation = DoorTileEncoding.DetectRotation(mapData, doorX, doorY);
        if (rotation == DoorRotation.HORIZONTAL)
        {
            if (TryPlanNeighbor(mapData, doorX + 1, doorY, WallFace.West, sideId, out var right))
                yield return right;
            if (TryPlanNeighbor(mapData, doorX - 1, doorY, WallFace.East, sideId, out var left))
                yield return left;
        }
        else
        {
            if (TryPlanNeighbor(mapData, doorX, doorY + 1, WallFace.North, sideId, out var below))
                yield return below;
            if (TryPlanNeighbor(mapData, doorX, doorY - 1, WallFace.South, sideId, out var above))
                yield return above;
        }
    }

    private static bool TryPlanNeighbor(
        MapData mapData, int x, int y, WallFace face, uint sideId, out FaceAssignment assignment)
    {
        assignment = default;
        if (mapData.GetTile(mapData.Walls, x, y) == 0)
            return false;

        assignment = new FaceAssignment(x, y, face, sideId);
        return true;
    }

    private static void ApplyAssignment(MapData mapData, FaceAssignment assignment)
    {
        var faceOverride = mapData.FindWallFaceOverride(assignment.TileX, assignment.TileY);
        if (faceOverride == null)
        {
            faceOverride = new WallFaceOverride
            {
                TileX = assignment.TileX,
                TileY = assignment.TileY
            };
            mapData.WallFaceOverrides.Add(faceOverride);
        }

        faceOverride.Set(assignment.Face, assignment.TileId);
    }
}
