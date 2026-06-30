using System.Numerics;
using Game.Core.Level;
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
        TestRecFileValidator();
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
            PlayerSnapshot = new PlayerSnapshot
            {
                PositionX = 1f,
                PositionY = 2f,
                PositionZ = 3f,
                ForwardX = 0f,
                ForwardY = 0f,
                ForwardZ = 1f
            },
            Events = events
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
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
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

    private sealed class TestTarget
    {
        public float MoveSpeed { get; set; }
        public Vector3 Position { get; set; }
    }
}
