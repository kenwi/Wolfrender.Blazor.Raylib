using Game.Core.Level;
using Game.Features.Pickups;
using Game.Features.Players;

namespace Game.Features.Enemies;

/// <summary>
/// Decodes Wolfenstein 3D plane-1 thing values into enemy placements, player
/// start, and ECWolf-style patrol paths from directional markers (90..97).
/// Thing numbers match ScanInfoPlane() in WL_GAME.C / WL_DEF.H.
/// </summary>
public static class W3dmapEnemyImport
{
    private const ushort DeadGuard = 124;

    private static readonly Dictionary<ushort, string> PlayerStarts = new()
    {
        { 19, "north" },
        { 20, "east" },
        { 21, "south" },
        { 22, "west" },
    };

    // Facing deltas for patrol markers 90..97 (east, NE, north, NW, west, SW, south, SE).
    private static readonly (int Dx, int Dy)[] PatrolMarkerDeltas =
    [
        (1, 0), (1, -1), (0, -1), (-1, -1),
        (-1, 0), (-1, 1), (0, 1), (1, 1),
    ];

    private static readonly (int Dx, int Dy)[] CardinalDeltas =
    [
        (1, 0),  // east
        (0, -1), // north
        (-1, 0), // west
        (0, 1),  // south
    ];

    private static readonly string[] CardinalFacings = ["east", "north", "west", "south"];

    private static readonly (ushort Base, string EnemyType, bool MutantSkillSteps)[] EnemyGroups =
    [
        (108, "Guard", false),
        (116, "Officer", false),
        (126, "Ss", false),
        (134, "Dog", false),
        (216, "Mutant", true),
    ];

    private static readonly Dictionary<ushort, string> Bosses = new()
    {
        { 160, "Boss" }, // Fake Hitler
        { 178, "Boss" }, // Hitler
        { 179, "Boss" }, // Fettgesicht
        { 196, "Boss" }, // Schabbs
        { 197, "Boss" }, // Gretel
        { 214, "Boss" }, // Hans
        { 215, "Boss" }, // Giftmacher
    };

    /// <summary>
    /// Gun-carrying soldiers that drop a used clip in Wolf3D (not dogs or bosses).
    /// </summary>
    private static readonly HashSet<string> AmmoDropEnemyTypes =
        new(StringComparer.Ordinal) { "Guard", "Officer", "Ss", "Mutant" };

    public readonly record struct DecodedThing(
        int TileX,
        int TileY,
        string EnemyType,
        string Stance,
        string Facing,
        bool StartsAsCorpse);

    /// <summary>
    /// Apply plane-1 actors onto an already plane-0-loaded <paramref name="mapData"/>.
    /// Overrides spawn when a player start is present. Builds patrol paths for
    /// patrol-stance enemies using markers 90..97 when available.
    /// </summary>
    public static void ApplyPlane1(MapData mapData, ReadOnlySpan<ushort> things, ReadOnlySpan<ushort> walls)
    {
        int side = mapData.Width;
        if (side <= 0 || mapData.Height != side)
            throw new InvalidOperationException("W3DMAP plane 1 requires a square map matching plane 0.");
        if (things.Length != side * side || walls.Length != side * side)
            throw new InvalidOperationException("W3DMAP plane 1 size must match plane 0.");

        var enemyTable = BuildEnemyTable();
        var markers = new Dictionary<(int X, int Y), (int Dx, int Dy)>();
        var decodedEnemies = new List<DecodedThing>();
        PlayerSpawnPlacement? playerStart = null;

        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                ushort thing = things[y * side + x];
                if (thing == 0)
                    continue;

                if (PlayerStarts.TryGetValue(thing, out string? facing))
                {
                    playerStart = new PlayerSpawnPlacement
                    {
                        TileX = x,
                        TileY = y,
                        WorldY = 2f,
                        Rotation = FacingToRotation(facing)
                    };
                    continue;
                }

                if (thing is >= 90 and <= 97)
                {
                    markers[(x, y)] = PatrolMarkerDeltas[thing - 90];
                    continue;
                }

                if (thing == DeadGuard)
                {
                    decodedEnemies.Add(new DecodedThing(x, y, "Guard", "stand", "east", StartsAsCorpse: true));
                    continue;
                }

                if (enemyTable.TryGetValue(thing, out var enemy))
                {
                    decodedEnemies.Add(new DecodedThing(
                        x, y, enemy.EnemyType, enemy.Stance, enemy.Facing, StartsAsCorpse: false));
                    continue;
                }

                if (Bosses.TryGetValue(thing, out string? bossType))
                {
                    decodedEnemies.Add(new DecodedThing(x, y, bossType, "boss", "east", StartsAsCorpse: false));
                    continue;
                }

                // Pushwalls (98), exit triggers (99), items, and ghosts are ignored for now.
            }
        }

        mapData.Enemies = new List<EnemyPlacement>(decodedEnemies.Count);
        foreach (var decoded in decodedEnemies)
        {
            var placement = new EnemyPlacement
            {
                TileX = decoded.TileX,
                TileY = decoded.TileY,
                Rotation = FacingToRotation(decoded.Facing),
                EnemyType = decoded.EnemyType,
                StartsAsCorpse = decoded.StartsAsCorpse,
                ShowPatrolPath = true,
                DropsAmmo = !decoded.StartsAsCorpse && AmmoDropEnemyTypes.Contains(decoded.EnemyType),
            };

            if (decoded.Stance == "patrol")
            {
                placement.PatrolPath = BuildPatrolPath(
                    decoded.TileX, decoded.TileY, decoded.Facing, markers, walls, side);
            }

            mapData.Enemies.Add(placement);

            // Pre-placed corpses already "dropped" their clip in Wolf3D.
            if (decoded.StartsAsCorpse && AmmoDropEnemyTypes.Contains(decoded.EnemyType))
            {
                mapData.Pickups.Add(new PickupPlacement
                {
                    TileX = decoded.TileX,
                    TileY = decoded.TileY,
                    Type = PickupType.GuardUsedAmmo,
                    Amount = 0
                });
            }
        }

        if (playerStart != null)
            mapData.Spawn = playerStart;
    }

    public static float FacingToRotation(string facing) => facing switch
    {
        "east" => 0f,
        "north" => -MathF.PI / 2f,
        "west" => MathF.PI,
        "south" => MathF.PI / 2f,
        _ => -MathF.PI / 2f
    };

    private static Dictionary<ushort, (string EnemyType, string Stance, string Facing)> BuildEnemyTable()
    {
        var table = new Dictionary<ushort, (string, string, string)>();
        foreach (var (baseId, enemyType, mutantSteps) in EnemyGroups)
        {
            var skillOffsets = mutantSteps
                ? new (int Offset, string _)[] { (0, "any"), (18, "medium+"), (36, "hard") }
                : new (int Offset, string _)[] { (0, "any"), (36, "medium+"), (72, "hard") };

            foreach (var (offset, _) in skillOffsets)
            {
                for (int direction = 0; direction < 4; direction++)
                {
                    table[(ushort)(baseId + offset + direction)] =
                        (enemyType, "stand", CardinalFacings[direction]);
                    table[(ushort)(baseId + offset + 4 + direction)] =
                        (enemyType, "patrol", CardinalFacings[direction]);
                }
            }
        }

        return table;
    }

    /// <summary>
    /// Walk from a patrol enemy along its facing, turning at markers 90..97,
    /// and collect marker tiles as waypoints (ECWolf-style). The enemy origin
    /// is omitted; <see cref="EnemySystem"/> treats it as the final loop stop.
    /// </summary>
    private static List<PatrolWaypoint> BuildPatrolPath(
        int startX,
        int startY,
        string facing,
        Dictionary<(int X, int Y), (int Dx, int Dy)> markers,
        ReadOnlySpan<ushort> walls,
        int side)
    {
        int facingIndex = Array.IndexOf(CardinalFacings, facing);
        if (facingIndex < 0)
            return new List<PatrolWaypoint>();

        int dx = CardinalDeltas[facingIndex].Dx;
        int dy = CardinalDeltas[facingIndex].Dy;
        int x = startX;
        int y = startY;

        var waypoints = new List<PatrolWaypoint>();
        var visitedStates = new HashSet<(int X, int Y, int Dx, int Dy)>();

        for (int step = 0; step < side * side; step++)
        {
            if (!visitedStates.Add((x, y, dx, dy)))
                break;

            if (markers.TryGetValue((x, y), out var turn))
            {
                if (!(x == startX && y == startY))
                    waypoints.Add(new PatrolWaypoint { TileX = x, TileY = y });
                dx = turn.Dx;
                dy = turn.Dy;
            }

            int nx = x + dx;
            int ny = y + dy;
            if (!IsWalkable(walls, side, nx, ny))
                break;

            x = nx;
            y = ny;

            if (x == startX && y == startY)
                break;
        }

        return waypoints;
    }

    /// <summary>Plane-0 walkability: solid walls are 1..63; doors and areas are open.</summary>
    private static bool IsWalkable(ReadOnlySpan<ushort> walls, int side, int x, int y)
    {
        if (x < 0 || y < 0 || x >= side || y >= side)
            return false;

        ushort tile = walls[y * side + x];
        return tile is < 1 or > 63;
    }
}
