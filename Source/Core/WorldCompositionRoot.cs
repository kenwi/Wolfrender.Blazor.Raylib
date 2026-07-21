using Game.DebugConsole;
using Game.Engine.Movement;
using Game.Engine.Simulation;
using Game.Features.Animation;
using Game.Features.Combat;
using Game.Features.Doors;
using Game.Features.Enemies;
using Game.Features.Highscores;
using Game.Features.Hud;
using Game.Features.LevelProgress;
using Game.Features.Options;
using Game.Features.Pickups;
using Game.Features.Players;
using Game.Features.Recording;
using Game.Features.SoundPropagation;
using Game.Features.WorldObjects;
using Raylib_cs;

namespace Game.Core;

/// <summary>
/// Constructed play-mode system graph. Owned by <see cref="World"/>.
/// </summary>
public sealed class WorldComposition
{
    public required MapData MapData { get; init; }
    public required LevelData Level { get; init; }
    public required List<Texture2D> TileTextures { get; init; }
    public required List<Texture2D> GameTextures { get; init; }
    public required Player Player { get; init; }

    public required ScoreSignals ScoreSignals { get; init; }
    public required OptionsMenuSystem OptionsMenu { get; init; }
    public required ControlsIntroSystem ControlsIntro { get; init; }
    public required FixedSimulationClock SimulationClock { get; init; }
    public required TickDiagnostics TickDiagnostics { get; init; }
    public required LightOcclusionMap LightOcclusionMap { get; init; }

    public required SoundSystem SoundSystem { get; init; }
    public required EffectSystem EffectSystem { get; init; }
    public required CombatFeedback CombatFeedback { get; init; }
    public required InputSystem InputSystem { get; init; }
    public required RecordingSystem RecordingSystem { get; init; }
    public required DoorSystem DoorSystem { get; init; }
    public required ScoreSystem ScoreSystem { get; init; }
    public required SecretSystem SecretSystem { get; init; }
    public required CollisionSystem CollisionSystem { get; init; }
    public required SoundPropagationSystem SoundPropagationSystem { get; init; }
    public required CameraSystem CameraSystem { get; init; }
    public required PlayOptionsFacade PlayOptions { get; init; }
    public required RenderSystem RenderSystem { get; init; }
    public required MinimapSystem MinimapSystem { get; init; }
    public required ExitSystem ExitSystem { get; init; }
    public required HighscoreClient HighscoreClient { get; init; }
    public required PickupSystem PickupSystem { get; init; }
    public required PlacedObjectSystem PlacedObjectSystem { get; init; }
    public required EnemySystem EnemySystem { get; init; }
    public required AnimationSystem AnimationSystem { get; init; }
    public required WeaponSystem WeaponSystem { get; init; }
    public required PlayerSystem PlayerSystem { get; init; }
    public required ConsoleOverlay ConsoleOverlay { get; init; }
}

/// <summary>
/// Builds the play-mode system graph before World host callbacks are available.
/// Session composers that need <see cref="World"/> methods are wired by World after construction.
/// </summary>
public static class WorldCompositionRoot
{
    public static WorldComposition Create(MapData mapData)
    {
        var level = new LevelData(mapData);
        var tileTextures = mapData.TileTextures;
        var gameTextures = mapData.GameTextures;
        var player = new Player();

        var scoreSignals = new ScoreSignals();
        var optionsMenu = new OptionsMenuSystem();
        var controlsIntro = new ControlsIntroSystem();
        var simulationClock = new FixedSimulationClock();
        var tickDiagnostics = new TickDiagnostics();
        var lightOcclusionMap = new LightOcclusionMap();

        var soundSystem = new SoundSystem(Res.Path("resources/03.mp3"));
        var effectSystem = new EffectSystem();
        var combatFeedback = new CombatFeedback(soundSystem, effectSystem);
        var inputSystem = new InputSystem();
        var recordingSystem = new RecordingSystem(inputSystem);
        var doorSystem = new DoorSystem(mapData.Doors, mapData.Width, tileTextures);
        var scoreSystem = new ScoreSystem(scoreSignals);
        var secretSystem = new SecretSystem(scoreSignals, tileTextures);
        var collisionSystem = new CollisionSystem(
            level,
            new CompositeMovementBlocker(doorSystem, secretSystem),
            ObjectCollisionRules.Instance);
        var soundPropagationSystem = new SoundPropagationSystem(mapData, doorSystem);
        var cameraSystem = new CameraSystem(collisionSystem);
        var playOptions = new PlayOptionsFacade(optionsMenu, soundSystem, cameraSystem);
        var renderSystem = new RenderSystem(level, mapData, tileTextures, DoorTileEncoding.ForEngine);
        var minimapSystem = new MinimapSystem(level, renderSystem);
        var exitSystem = new ExitSystem(scoreSystem);
        var highscoreClient = new HighscoreClient();
        var pickupSystem = new PickupSystem(scoreSignals);
        var placedObjectSystem = new PlacedObjectSystem();
        var enemySystem = new EnemySystem(
            player, inputSystem, collisionSystem, doorSystem, combatFeedback,
            pickupSystem, scoreSignals, soundPropagationSystem);

        pickupSystem.SetObjectsTexture(gameTextures[GameTextureIndex.Objects]);
        pickupSystem.Rebuild(mapData.Pickups, mapData);
        placedObjectSystem.SetObjectsTexture(gameTextures[GameTextureIndex.Objects]);
        placedObjectSystem.Rebuild(mapData);

        var animationSystem = new AnimationSystem(
            gameTextures[GameTextureIndex.EnemyGuard],
            gameTextures[GameTextureIndex.Weapons],
            player,
            enemySystem);
        var weaponSystem = new WeaponSystem(
            mapData,
            doorSystem,
            enemySystem,
            gameTextures[GameTextureIndex.EnemyGuard],
            effectSystem,
            soundSystem,
            animationSystem,
            soundPropagationSystem);
        var playerSystem = new PlayerSystem(
            player,
            inputSystem,
            collisionSystem,
            cameraSystem,
            pickupSystem,
            doorSystem,
            animationSystem,
            enemySystem,
            weaponSystem,
            effectSystem,
            exitSystem,
            secretSystem);

        return new WorldComposition
        {
            MapData = mapData,
            Level = level,
            TileTextures = tileTextures,
            GameTextures = gameTextures,
            Player = player,
            ScoreSignals = scoreSignals,
            OptionsMenu = optionsMenu,
            ControlsIntro = controlsIntro,
            SimulationClock = simulationClock,
            TickDiagnostics = tickDiagnostics,
            LightOcclusionMap = lightOcclusionMap,
            SoundSystem = soundSystem,
            EffectSystem = effectSystem,
            CombatFeedback = combatFeedback,
            InputSystem = inputSystem,
            RecordingSystem = recordingSystem,
            DoorSystem = doorSystem,
            ScoreSystem = scoreSystem,
            SecretSystem = secretSystem,
            CollisionSystem = collisionSystem,
            SoundPropagationSystem = soundPropagationSystem,
            CameraSystem = cameraSystem,
            PlayOptions = playOptions,
            RenderSystem = renderSystem,
            MinimapSystem = minimapSystem,
            ExitSystem = exitSystem,
            HighscoreClient = highscoreClient,
            PickupSystem = pickupSystem,
            PlacedObjectSystem = placedObjectSystem,
            EnemySystem = enemySystem,
            AnimationSystem = animationSystem,
            WeaponSystem = weaponSystem,
            PlayerSystem = playerSystem,
            ConsoleOverlay = new ConsoleOverlay()
        };
    }
}
