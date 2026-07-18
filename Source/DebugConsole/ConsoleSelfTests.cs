using System.Numerics;
using Game.Core.Level;
using Game.Engine.Rendering;
using Game.Features.Doors;
using Game.Features.LevelProgress;
using Game.Features.Recording;

namespace Game.DebugConsole;

public static class ConsoleSelfTests
{
    private static bool _hasRun;

    public static void RunOnce()
    {
        if (_hasRun)
            return;
        _hasRun = true;

        TestParserQuotedArgs();
        TestStringVariableStore();
        TestRuntimeAccessorGetSet();
        TestLevelCatalogNormalizePath();
        TestObjectSpritesLayout();
        TestSecretWallJsonRoundTrip();
        TestRecFileRoundTrip();
        TestRecFileLegacyParse();
        TestSimulationChecksumDiff();
        TestRecFileValidator();
        TestLevelRoomMapDoorVisibility();
        TestConsoleTextSelectionNormalizeAndExtract();
        TestConsoleTextSelectionReplaceInputRange();
        TestConsoleTextSelectionFirstPasteLine();
        TestConsoleTextSelectionGetWordRange();
        TestConsoleTextSelectionFullLineExtend();
        TestConsoleTextSelectionWordExtend();
    }

    private static void TestConsoleTextSelectionWordExtend()
    {
        const string line = "set Player.Speed 1.5";

        if (ConsoleTextSelection.TryMoveWordBoundary(line, 16, 1) != 20)
            throw new InvalidOperationException("Right from word end should land on next word end.");
        if (ConsoleTextSelection.TryMoveWordBoundary(line, 4, -1) != 0)
            throw new InvalidOperationException("Left from word start should land on previous word start.");
        if (ConsoleTextSelection.TryMoveWordBoundary(line, 0, -1) is not null)
            throw new InvalidOperationException("Left at start of line should not move.");
        if (ConsoleTextSelection.TryMoveWordBoundary(line, 20, 1) is not null)
            throw new InvalidOperationException("Right at end of line should not move.");

        var (snapStart, snapEnd) = ConsoleTextSelection.SnapToWordBounds(line, 6, 10);
        if (snapStart != 4 || snapEnd != 16)
            throw new InvalidOperationException("SnapToWordBounds should expand to the full word.");

        var (anchorRight, focusRight) = ConsoleTextSelection.BuildWordSelection(0, 4, 16, 0, 20);
        if (anchorRight.CharIndex != 4 || focusRight.CharIndex != 20)
            throw new InvalidOperationException("Rightward word selection should grow from origin start.");

        var (anchorLeft, focusLeft) = ConsoleTextSelection.BuildWordSelection(0, 4, 16, 0, 0);
        if (anchorLeft.CharIndex != 16 || focusLeft.CharIndex != 0)
            throw new InvalidOperationException("Leftward word selection should grow from origin end.");
    }

    private static void TestConsoleTextSelectionFullLineExtend()
    {
        var (anchorUp, focusUp) = ConsoleTextSelection.BuildFullLineSelection(5, 3, 10, 8);
        if (anchorUp.LineIndex != 5 || anchorUp.CharIndex != 10
            || focusUp.LineIndex != 3 || focusUp.CharIndex != 0)
            throw new InvalidOperationException("Upward full-line selection should pin origin end and focus start.");

        var (anchorDown, focusDown) = ConsoleTextSelection.BuildFullLineSelection(5, 7, 10, 4);
        if (anchorDown.LineIndex != 5 || anchorDown.CharIndex != 0
            || focusDown.LineIndex != 7 || focusDown.CharIndex != 4)
            throw new InvalidOperationException("Downward full-line selection should pin origin start and focus end.");

        var (anchorSame, focusSame) = ConsoleTextSelection.BuildFullLineSelection(2, 2, 6, 6);
        if (anchorSame.CharIndex != 0 || focusSame.CharIndex != 6 || anchorSame.LineIndex != 2)
            throw new InvalidOperationException("Same-line full selection should cover the whole line.");

        if (ConsoleTextSelection.TryMoveLineIndex(0, -1, 5) is not null)
            throw new InvalidOperationException("Cannot move up from first scrollback line.");
        if (ConsoleTextSelection.TryMoveLineIndex(4, 1, 5) != ConsoleTextPos.InputLineIndex)
            throw new InvalidOperationException("Down from last scrollback should reach input.");
        if (ConsoleTextSelection.TryMoveLineIndex(ConsoleTextPos.InputLineIndex, -1, 5) != 4)
            throw new InvalidOperationException("Up from input should reach last scrollback line.");
        if (ConsoleTextSelection.TryMoveLineIndex(ConsoleTextPos.InputLineIndex, 1, 5) is not null)
            throw new InvalidOperationException("Cannot move down from input line.");
    }

    private static void TestConsoleTextSelectionGetWordRange()
    {
        const string line = "set Player.Speed 1.5";
        var (start, end) = ConsoleTextSelection.GetWordRange(line, 6);
        if (start != 4 || end != 16 || line[start..end] != "Player.Speed")
            throw new InvalidOperationException($"GetWordRange mismatch: [{start},{end})='{line[start..end]}'.");

        var (wsStart, wsEnd) = ConsoleTextSelection.GetWordRange(line, 3);
        if (wsStart != 3 || wsEnd != 4 || line[wsStart..wsEnd] != " ")
            throw new InvalidOperationException("GetWordRange should select the whitespace run.");

        var anchor = new ConsoleTextPos(0, 4);
        var focus = new ConsoleTextPos(0, 16);
        var pos = new ConsoleTextPos(0, 8);
        if (!ConsoleTextSelection.IsExactWordSelection(line, anchor, focus, pos))
            throw new InvalidOperationException("IsExactWordSelection should be true for matching word.");

        var otherPos = new ConsoleTextPos(0, 0);
        if (ConsoleTextSelection.IsExactWordSelection(line, anchor, focus, otherPos))
            throw new InvalidOperationException("IsExactWordSelection should be false for a different word.");
    }

    private static void TestConsoleTextSelectionNormalizeAndExtract()
    {
        var a = new ConsoleTextPos(0, 2);
        var b = new ConsoleTextPos(1, 3);
        var (start, end) = ConsoleTextSelection.Normalize(b, a);
        if (start.LineIndex != 0 || start.CharIndex != 2 || end.LineIndex != 1 || end.CharIndex != 3)
            throw new InvalidOperationException("ConsoleTextSelection.Normalize should order positions.");

        var scrollback = new List<string> { "abcdef", "ghijkl" };
        string extracted = ConsoleTextSelection.Extract(scrollback, "input", a, b);
        if (extracted != "cdef\nghi")
            throw new InvalidOperationException($"Scrollback extract mismatch: '{extracted}'.");

        var inA = new ConsoleTextPos(ConsoleTextPos.InputLineIndex, 1);
        var inB = new ConsoleTextPos(ConsoleTextPos.InputLineIndex, 4);
        string inputExtract = ConsoleTextSelection.Extract(scrollback, "abcdef", inB, inA);
        if (inputExtract != "bcd")
            throw new InvalidOperationException($"Input extract mismatch: '{inputExtract}'.");

        if (ConsoleTextSelection.HasSelection(inA, inA))
            throw new InvalidOperationException("Identical positions should not count as a selection.");
        if (!ConsoleTextSelection.HasSelection(inA, inB))
            throw new InvalidOperationException("Distinct positions should count as a selection.");

        var mixedStart = new ConsoleTextPos(1, 2);
        var mixedEnd = new ConsoleTextPos(ConsoleTextPos.InputLineIndex, 3);
        string mixed = ConsoleTextSelection.Extract(scrollback, "abcdef", mixedStart, mixedEnd);
        if (mixed != "ijkl\nabc")
            throw new InvalidOperationException($"Mixed scrollback+input extract mismatch: '{mixed}'.");
    }

    private static void TestConsoleTextSelectionReplaceInputRange()
    {
        var buffer = new System.Text.StringBuilder("hello world");
        int cursor = ConsoleTextSelection.ReplaceInputRange(buffer, 6, 11, "there");
        if (buffer.ToString() != "hello there" || cursor != 11)
            throw new InvalidOperationException($"ReplaceInputRange mismatch: '{buffer}' cursor={cursor}.");

        cursor = ConsoleTextSelection.DeleteInputRange(buffer, 0, 6);
        if (buffer.ToString() != "there" || cursor != 0)
            throw new InvalidOperationException($"DeleteInputRange mismatch: '{buffer}' cursor={cursor}.");
    }

    private static void TestConsoleTextSelectionFirstPasteLine()
    {
        if (ConsoleTextSelection.FirstPasteLine("one\r\ntwo\nthree") != "one")
            throw new InvalidOperationException("FirstPasteLine should keep only the first line.");
        if (ConsoleTextSelection.FirstPasteLine("single") != "single")
            throw new InvalidOperationException("FirstPasteLine should preserve single-line paste.");
        if (ConsoleTextSelection.FirstPasteLine("") != string.Empty)
            throw new InvalidOperationException("FirstPasteLine should handle empty clipboard.");
    }

    private static void TestParserQuotedArgs()
    {
        var parser = new ConsoleCommandParser();
        var result = parser.TryParse("set Player.Position \"1, 2, 3\"", out var invocation);
        if (!result.Success || invocation == null)
            throw new InvalidOperationException("Console parser failed to parse quoted arguments.");

        if (!string.Equals(invocation.Name, "set", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Console parser command name mismatch.");
        if (invocation.Arguments.Count != 2)
            throw new InvalidOperationException("Console parser argument count mismatch.");
    }

    private static void TestStringVariableStore()
    {
        var store = new StringVariableStore();
        if (!store.TrySetValue("test.value", "abc", out _))
            throw new InvalidOperationException("StringVariableStore set failed.");
        if (!store.TryGetValue("test.value", out var value, out _))
            throw new InvalidOperationException("StringVariableStore get failed.");
        if (value != "abc")
            throw new InvalidOperationException("StringVariableStore roundtrip mismatch.");
    }

    private static void TestLevelCatalogNormalizePath()
    {
        if (LevelCatalog.NormalizePath("test") != "resources/test.json")
            throw new InvalidOperationException("LevelCatalog should prefix resources/ and .json.");
        if (LevelCatalog.NormalizePath("resources/foo.json") != "resources/foo.json")
            throw new InvalidOperationException("LevelCatalog should preserve explicit paths.");
    }

    private static void TestRuntimeAccessorGetSet()
    {
        var target = new TestTarget { MoveSpeed = 5f, Position = new Vector3(1, 2, 3) };
        var accessor = new RuntimeVariableAccessor(
            new[]
            {
                new RootBinding
                {
                    Name = "Test",
                    Resolver = index => index.HasValue
                        ? ResolveResult.Fail("Index not supported")
                        : ResolveResult.Ok(target),
                    CuratedListFactory = () => new[] { "Test.MoveSpeed", "Test.Position" },
                    DiscoveryFactory = () => new[] { "Test.MoveSpeed", "Test.Position" }
                }
            },
            new[] { "Test.MoveSpeed", "Test.Position" });

        if (!accessor.TrySetValue("Test.MoveSpeed", "9.5", out _))
            throw new InvalidOperationException("Runtime accessor failed to set float.");
        if (!accessor.TryGetValue("Test.MoveSpeed", out var moveSpeed, out _))
            throw new InvalidOperationException("Runtime accessor failed to get float.");
        if (moveSpeed != "9.5")
            throw new InvalidOperationException("Runtime accessor float roundtrip mismatch.");

        if (!accessor.TrySetValue("Test.Position", "4,5,6", out _))
            throw new InvalidOperationException("Runtime accessor failed to set Vector3.");
        if (!accessor.TryGetValue("Test.Position", out var position, out _))
            throw new InvalidOperationException("Runtime accessor failed to get Vector3.");
        if (position != "4,5,6 tile=(1,1)")
            throw new InvalidOperationException($"Runtime accessor vector roundtrip mismatch: '{position}'.");
    }

    private static void TestObjectSpritesLayout()
    {
        var rect = ObjectSprites.GetFrameRect(0);
        if (rect.X != ObjectSprites.OriginX || rect.Y != ObjectSprites.OriginY
            || rect.Width != ObjectSprites.FrameSize || rect.Height != ObjectSprites.FrameSize)
            throw new InvalidOperationException("ObjectSprites.GetFrameRect(0) layout mismatch.");

        if (ObjectSprites.ObjectCount != 20)
            throw new InvalidOperationException("ObjectSprites.ObjectCount should be 20.");

        if (!ObjectSprites.IsValidObjectId(1) || !ObjectSprites.IsValidObjectId(20)
            || ObjectSprites.IsValidObjectId(0) || ObjectSprites.IsValidObjectId(21))
            throw new InvalidOperationException("ObjectSprites.IsValidObjectId range mismatch.");
    }

    private static void TestSecretWallJsonRoundTrip()
    {
        var mapData = new MapData
        {
            Width = 4,
            Height = 4,
            Floor = new uint[16],
            Walls = new uint[16],
            Ceiling = new uint[16],
            Doors = new uint[16],
            Objects = new uint[16],
            SecretWalls =
            {
                new SecretWallPlacement
                {
                    TileX = 2,
                    TileY = 1,
                    Direction = SecretWallDirection.East,
                    TravelTiles = 3
                }
            }
        };

        var json = LevelSerializer.SerializeToJson(mapData);
        var loaded = new MapData
        {
            Floor = new uint[16],
            Walls = new uint[16],
            Ceiling = new uint[16],
            Doors = new uint[16],
            Objects = new uint[16]
        };
        LevelSerializer.DeserializeFromJson(loaded, json);

        if (loaded.SecretWalls.Count != 1)
            throw new InvalidOperationException("SecretWalls count mismatch after JSON round-trip.");
        var secret = loaded.SecretWalls[0];
        if (secret.TileX != 2 || secret.TileY != 1 || secret.Direction != SecretWallDirection.East || secret.TravelTiles != 3)
            throw new InvalidOperationException("SecretWalls field mismatch after JSON round-trip.");

        LevelSerializer.DeserializeFromJson(loaded, """{"Width":4,"Height":4,"Floor":[],"Walls":[],"Ceiling":[],"Doors":[],"Objects":[]}""");
        if (loaded.SecretWalls.Count != 0)
            throw new InvalidOperationException("Missing SecretWalls key should deserialize to empty list.");
    }

    private static void TestRecFileRoundTrip()
    {
        var events = new InputEvent[]
        {
            new KeyDownEvent(0f, GameplayKey.MoveForward) { Tick = 1 },
            new MouseDeltaEvent(0.016f, 2f, -1f) { Tick = 2 },
            new KeyUpEvent(0.5f, GameplayKey.MoveForward) { Tick = 30 }
        };

        var file = new RecFile
        {
            Version = RecFile.CurrentVersion,
            LevelPath = "resources/test.json",
            MouseSensitivity = 1.25f,
            TickHz = 60,
            DurationTicks = 120,
            RngSeed = 12345,
            PlayerSnapshot = new PlayerSnapshot
            {
                PositionX = 1f,
                PositionY = 2f,
                PositionZ = 3f,
                ForwardX = 0f,
                ForwardY = 0f,
                ForwardZ = 1f
            },
            Events = events,
            Checksums = new[]
            {
                new ChecksumKeyframe(60, 0xDEADBEEF, 0x12345678, 0x0000FFFF, 0xABCDEF01),
                new ChecksumKeyframe(120, 1, 2, 3, 4)
            }
        };

        string path = Path.Combine(Path.GetTempPath(), $"wolfrender-rec-test-{Guid.NewGuid():N}.rec");
        try
        {
            RecFileSerializer.Write(path, file);
            var loaded = RecFileSerializer.Read(path);
            if (loaded.LevelPath != file.LevelPath || loaded.MouseSensitivity != file.MouseSensitivity)
                throw new InvalidOperationException("RecFile header round-trip mismatch.");
            if (loaded.ResolveTickHz() != 60)
                throw new InvalidOperationException("RecFile tickHz round-trip mismatch.");
            if (!loaded.UsesTickIndexedEvents)
                throw new InvalidOperationException("RecFile should use tick-indexed events.");
            if (loaded.Events[0].Tick != 1 || loaded.Events[^1].Tick != 30)
                throw new InvalidOperationException("RecFile event tick round-trip mismatch.");
            if (loaded.PlayerSnapshot?.PositionX != 1f || loaded.PlayerSnapshot?.ForwardZ != 1f)
                throw new InvalidOperationException("RecFile player snapshot round-trip mismatch.");
            if (loaded.Events.Count != events.Length)
                throw new InvalidOperationException("RecFile event count round-trip mismatch.");
            if (loaded.DurationTicks != 120 || loaded.ResolveDurationTicks() != 120)
                throw new InvalidOperationException("RecFile durationTicks round-trip mismatch.");
            if (loaded.RngSeed != 12345)
                throw new InvalidOperationException("RecFile rngSeed round-trip mismatch.");
            if (loaded.Checksums.Count != 2
                || loaded.Checksums[0] != new ChecksumKeyframe(60, 0xDEADBEEF, 0x12345678, 0x0000FFFF, 0xABCDEF01)
                || loaded.Checksums[1].Tick != 120)
                throw new InvalidOperationException("RecFile checksum round-trip mismatch.");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void TestRecFileLegacyParse()
    {
        // v4 file written before durationTicks/rngSeed/checksums existed.
        const string legacyJson = """
        {
            "version": 4,
            "levelPath": "resources/test.json",
            "mouseSensitivity": 1.0,
            "tickHz": 60,
            "events": [
                { "kind": "keyDown", "tick": 1, "time": 0.016, "key": "moveForward" },
                { "kind": "keyUp", "tick": 45, "time": 0.75, "key": "moveForward" }
            ]
        }
        """;

        var legacy = RecFileSerializer.Parse(legacyJson);
        if (legacy.Version != 4 || !legacy.UsesTickIndexedEvents)
            throw new InvalidOperationException("Legacy v4 recording parse mismatch.");
        if (legacy.DurationTicks != 0)
            throw new InvalidOperationException("Legacy recording should have no explicit durationTicks.");
        if (legacy.ResolveDurationTicks() != 45)
            throw new InvalidOperationException("Legacy recording should fall back to last event tick for duration.");
        if (legacy.RngSeed != null)
            throw new InvalidOperationException("Legacy recording should have no rngSeed.");
        if (legacy.Checksums.Count != 0)
            throw new InvalidOperationException("Legacy recording should have no checksums.");
    }

    private static void TestSimulationChecksumDiff()
    {
        var a = new ChecksumKeyframe(60, 1, 2, 3, 4);
        var same = new ChecksumKeyframe(60, 1, 2, 3, 4);
        var diff = new ChecksumKeyframe(60, 1, 99, 3, 44);

        if (!a.Matches(same) || a.DiffComponents(same).Count != 0)
            throw new InvalidOperationException("Identical checksums should match with no diffs.");

        if (a.Matches(diff))
            throw new InvalidOperationException("Different checksums should not match.");

        var components = a.DiffComponents(diff);
        if (components.Count != 2 || !components.Contains("enemies") || !components.Contains("score"))
            throw new InvalidOperationException("Checksum diff should name the diverged components.");
    }

    private static void TestRecFileValidator()
    {
        var valid = new RecFile
        {
            Version = RecFile.CurrentVersion,
            LevelPath = LevelCatalog.DefaultLevelPath,
            MouseSensitivity = 1f,
            TickHz = 60,
            Events = new InputEvent[] { new KeyDownEvent(0f, GameplayKey.MoveForward) { Tick = 1 } }
        };

        if (!RecFileValidator.TryValidateForReplay(valid, _ => true, out _))
            throw new InvalidOperationException("Valid recording should pass validation.");

        var badVersion = new RecFile
        {
            Version = 99,
            LevelPath = valid.LevelPath,
            MouseSensitivity = valid.MouseSensitivity,
            TickHz = valid.TickHz,
            Events = valid.Events
        };
        if (RecFileValidator.TryValidateForReplay(badVersion, _ => true, out _))
            throw new InvalidOperationException("Unsupported version should fail validation.");

        var badTick = new RecFile
        {
            Version = valid.Version,
            LevelPath = valid.LevelPath,
            MouseSensitivity = valid.MouseSensitivity,
            TickHz = valid.TickHz,
            Events = new InputEvent[] { new KeyDownEvent(0f, GameplayKey.MoveForward) { Tick = 0 } }
        };
        if (RecFileValidator.TryValidateForReplay(badTick, _ => true, out _))
            throw new InvalidOperationException("Invalid tick index should fail validation.");
    }

    private static void TestLevelRoomMapDoorVisibility()
    {
        const int doorX = 2;
        const int doorY = 3;
        var map = CreateTwoRoomDoorMap(doorX, doorY);
        var roomMap = LevelRoomMap.Build(map);

        if (roomMap.RoomCount != 2)
            throw new InvalidOperationException($"Two-room test map should flood-fill into 2 rooms, got {roomMap.RoomCount}.");

        var closedDoors = new List<Door>
        {
            new()
            {
                StartPosition = new Vector2(doorX, doorY),
                DoorState = DoorState.CLOSED
            }
        };

        var visible = roomMap.ComputeVisibleRooms(doorX, doorY, closedDoors);
        if (visible.Count < 2)
            throw new InvalidOperationException("Closed door tile should seed visibility for both adjacent rooms.");

        foreach (var link in roomMap.DoorLinks)
        {
            if (link.DoorTileX != doorX || link.DoorTileY != doorY)
                continue;

            if (!visible.Contains(link.RoomA) || !visible.Contains(link.RoomB))
                throw new InvalidOperationException("Both rooms linked by the door should be visible from the door tile.");
        }
    }

    /// <summary>
    /// 5-wide, 7-tall map split by a horizontal wall row at y=3 with a single door gap at (doorX, doorY).
    /// The door is the only connection between the north room and the south room.
    /// </summary>
    private static MapData CreateTwoRoomDoorMap(int doorX, int doorY)
    {
        const int width = 5;
        const int height = 7;
        const int dividerRow = 3;
        int tileCount = width * height;
        var map = new MapData
        {
            Width = width,
            Height = height,
            Floor = new uint[tileCount],
            Ceiling = new uint[tileCount],
            Walls = new uint[tileCount],
            Doors = new uint[tileCount]
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = LevelData.GetIndex(x, y, width);
                bool border = x == 0 || x == width - 1 || y == 0 || y == height - 1;
                bool divider = y == dividerRow;
                if (border || divider)
                {
                    map.Walls[index] = 1;
                    continue;
                }

                map.Floor[index] = 1;
                map.Ceiling[index] = 1;
            }
        }

        int doorIndex = LevelData.GetIndex(doorX, doorY, width);
        map.Walls[doorIndex] = 0;
        map.Doors[doorIndex] = DoorTileEncoding.LightHorizontal;
        return map;
    }

    private sealed class TestTarget
    {
        public float MoveSpeed { get; set; }
        public Vector3 Position { get; set; }
    }
}
