using System.Numerics;
using Game.Features.Enemies;
using Game.Features.LevelProgress;
using Game.Features.Players;
using Game.Features.Recording;

namespace Game.DebugConsole;

public static class WorldConsoleBindings
{
    public static RuntimeConsoleService CreateConsole(
        World world,
        Player player,
        EnemySystem enemySystem,
        ScoreSystem scoreSystem,
        ConsoleOverlay overlay,
        RecordingSystem recordingSystem,
        Func<float> getMouseSensitivity)
    {
        var runtimeAccessor = new RuntimeVariableAccessor(
            CreateRoots(world, player, enemySystem, scoreSystem),
            CreateWritableWhitelist());
        var stringStore = new StringVariableStore();
        var variables = new CompositeVariableAccessor(runtimeAccessor, stringStore);

        var output = new ConsoleOutputMultiplexer(new IConsoleOutput[]
        {
            new OverlayConsoleOutput(overlay),
            new DebugConsoleOutput(),
            new TerminalConsoleOutput()
        });

        return new RuntimeConsoleService(
            variables,
            output,
            world.LoadLevel,
            world.RestartCurrentLevel,
            () =>
            {
                overlay.ClearScrollback();
                return ConsoleCommandResult.Ok("Console output cleared. History (↑/↓) is unchanged.");
            },
            () => world.CurrentLevelPath,
            world.ListPickupsForConsole,
            filename => world.StartRecordingForConsole(filename, getMouseSensitivity()),
            recordingSystem.StopRecording,
            world.StartReplayForConsole,
            recordingSystem.StopReplay,
            recordingSystem.SendRecording,
            world.ToggleTickDiagnostics,
            world.SetTickDiagnostics,
            world.GetTickDiagnosticsStatus,
            world.ToggleStaticMeshes,
            world.SetStaticMeshes,
            world.GetStaticMeshesStatus,
            world.ToggleFlying,
            world.SetFlying,
            world.GetFlyingStatus,
            world.DumpLightingCheckForConsole);
    }

    private static IReadOnlyList<RootBinding> CreateRoots(
        World world,
        Player player,
        EnemySystem enemySystem,
        ScoreSystem scoreSystem)
    {
        var graphics = new GraphicsConsoleSettings(world);
        var audio = new AudioConsoleSettings(world);

        return new RootBinding[]
        {
            new()
            {
                Name = "Score",
                Resolver = index => index.HasValue
                    ? ResolveResult.Fail("Score root does not support indexing.")
                    : ResolveResult.Ok(scoreSystem),
                CuratedListFactory = () => new[]
                {
                    "Score.LevelScore",
                    "Score.Kills",
                    "Score.TotalKillableEnemies",
                    "Score.TreasuresCollected",
                    "Score.ElapsedActiveSeconds"
                },
                DiscoveryFactory = () => DiscoverVariablesForInstance(scoreSystem, "Score")
            },
            new()
            {
                Name = "Player",
                Resolver = index => index.HasValue
                    ? ResolveResult.Fail("Player root does not support indexing.")
                    : ResolveResult.Ok(player),
                CuratedListFactory = () => new[]
                {
                    "Player.MoveSpeed",
                    "Player.Position",
                    "Player.CollisionRadius",
                    "Player.Ammo",
                    "Player.Weapons.ActiveWeapon",
                    "Player.HasMachineGun",
                    "Player.HasChainGun",
                    "Player.HasGoldKey",
                    "Player.HasSilverKey"
                },
                DiscoveryFactory = () => DiscoverVariablesForInstance(player, "Player")
            },
            new()
            {
                Name = "Enemy",
                Resolver = index =>
                {
                    if (!index.HasValue)
                        return ResolveResult.Fail("Enemy root requires an index. Example: Enemy[0].MoveSpeed");

                    var enemies = enemySystem.Enemies;
                    if (index.Value < 0 || index.Value >= enemies.Count)
                        return ResolveResult.Fail($"Enemy index out of range: {index.Value} (count={enemies.Count})");

                    return ResolveResult.Ok(enemies[index.Value]);
                },
                CuratedListFactory = () => new[]
                {
                    "Enemy[index].MoveSpeed",
                    "Enemy[index].Position",
                    "Enemy[index].Rotation",
                    "Enemy[index].EnemyState",
                    "Enemy[index].CorpseLingerSeconds",
                    "Enemy[index].HitReactionDurationSeconds"
                },
                DiscoveryFactory = () => DiscoverVariablesForType(typeof(Enemy), "Enemy[index]")
            },
            new()
            {
                Name = "RenderData",
                Resolver = index => index.HasValue
                    ? ResolveResult.Fail("RenderData root does not support indexing.")
                    : ResolveResult.Ok(graphics),
                CuratedListFactory = () => new[]
                {
                    "RenderData.InternalWidth",
                    "RenderData.InternalHeight",
                    "RenderData.FullscreenEnabled",
                    "RenderData.WindowResolutionPresetId",
                    "RenderData.GameResolutionPresetId",
                    "RenderData.VSyncEnabled",
                    "RenderData.TargetFps"
                },
                DiscoveryFactory = () => DiscoverVariablesForInstance(graphics, "RenderData")
            },
            new()
            {
                Name = "Audio",
                Resolver = index => index.HasValue
                    ? ResolveResult.Fail("Audio root does not support indexing.")
                    : ResolveResult.Ok(audio),
                CuratedListFactory = () => new[]
                {
                    "Audio.Volume",
                    "volume"
                },
                DiscoveryFactory = () => DiscoverVariablesForInstance(audio, "Audio")
            }
        };
    }

    private static IReadOnlyList<string> CreateWritableWhitelist()
    {
        return new[]
        {
            "Player.MoveSpeed",
            "Player.Position",
            "Player.CollisionRadius",
            "Player.Ammo",
            "Player.Weapons.ActiveWeapon",
            "Player.HasMachineGun",
            "Player.HasGoldKey",
            "Player.HasSilverKey",
            "Enemy[index].MoveSpeed",
            "Enemy[index].Position",
            "Enemy[index].Rotation",
            "Enemy[index].EnemyState",
            "Enemy[index].CorpseLingerSeconds",
            "Enemy[index].HitReactionDurationSeconds",
            "RenderData.InternalWidth",
            "RenderData.InternalHeight",
            "RenderData.FullscreenEnabled",
            "RenderData.WindowResolutionPresetId",
            "RenderData.GameResolutionPresetId",
            "RenderData.VSyncEnabled",
            "RenderData.TargetFps",
            "Audio.Volume"
        };
    }

    private static IReadOnlyList<string> DiscoverVariablesForType(Type type, string prefix)
    {
        var results = new List<string>();
        foreach (var property in type.GetProperties().Where(p => p.GetMethod != null))
        {
            if (!IsSupportedTerminalType(property.PropertyType))
                continue;

            results.Add($"{prefix}.{property.Name}");
        }

        return results.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> DiscoverVariablesForInstance(object instance, string prefix)
    {
        var type = instance.GetType();
        return DiscoverVariablesForType(type, prefix);
    }

    private static bool IsSupportedTerminalType(Type type)
    {
        var effectiveType = Nullable.GetUnderlyingType(type) ?? type;
        if (effectiveType.IsEnum)
            return true;

        return effectiveType == typeof(string)
            || effectiveType == typeof(int)
            || effectiveType == typeof(float)
            || effectiveType == typeof(double)
            || effectiveType == typeof(bool)
            || effectiveType == typeof(Vector2)
            || effectiveType == typeof(Vector3);
    }
}

public sealed class GraphicsConsoleSettings
{
    private readonly World _world;

    public GraphicsConsoleSettings(World world)
    {
        _world = world;
    }

    public int InternalWidth => RenderData.InternalWidth;

    public int InternalHeight => RenderData.InternalHeight;

    public bool FullscreenEnabled
    {
        get => _world.GetFullscreenEnabled();
        set => _world.SetFullscreenEnabled(value);
    }

    public string WindowResolutionPresetId
    {
        get => _world.GetWindowResolutionPresetId();
        set => _world.SetWindowResolutionPresetId(value);
    }

    public string GameResolutionPresetId
    {
        get => _world.GetGameResolutionPresetId();
        set => _world.SetGameResolutionPresetId(value);
    }

    public bool VSyncEnabled
    {
        get => _world.GetVSyncEnabled();
        set => _world.SetVSyncEnabled(value);
    }

    public int TargetFps
    {
        get => _world.GetTargetFps();
        set => _world.SetTargetFps(value);
    }
}

public sealed class AudioConsoleSettings
{
    private readonly World _world;

    public AudioConsoleSettings(World world)
    {
        _world = world;
    }

    public float Volume
    {
        get => _world.GetVolume();
        set => _world.SetVolume(value);
    }
}
