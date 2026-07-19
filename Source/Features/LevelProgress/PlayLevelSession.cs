using Game.DebugConsole;
using Game.Editor;
using Game.Engine.Rendering;
using Game.Engine.Simulation;
using Game.Features.Combat;
using Game.Features.Doors;
using Game.Features.Enemies;
using Game.Features.Highscores;
using Game.Features.Hud;
using Game.Features.Options;
using Game.Features.Pickups;
using Game.Features.Players;
using Game.Features.Recording;
using Game.Features.SoundPropagation;
using Game.Features.WorldObjects;

namespace Game.Features.LevelProgress;

/// <summary>
/// Play-mode level path, load/restart/reset, and enter rebuild sequence.
/// </summary>
public sealed class PlayLevelSession
{
    private readonly MapData _mapData;
    private readonly PlayerSystem _playerSystem;
    private readonly DoorSystem _doorSystem;
    private readonly EnemySystem _enemySystem;
    private readonly PickupSystem _pickupSystem;
    private readonly PlacedObjectSystem _placedObjectSystem;
    private readonly ScoreSystem _scoreSystem;
    private readonly ExitSystem _exitSystem;
    private readonly SecretSystem _secretSystem;
    private readonly RenderSystem _renderSystem;
    private readonly SoundPropagationSystem _soundPropagationSystem;
    private readonly HighscoreIntermission _highscoreIntermission;
    private readonly HighscoreClient _highscoreClient;
    private readonly EffectSystem _effectSystem;
    private readonly RecordingSystem _recordingSystem;
    private readonly FixedSimulationClock _simulationClock;
    private readonly TickDiagnostics _tickDiagnostics;
    private readonly InputSystem _inputSystem;
    private readonly ControlsIntroSystem _controlsIntro;
    private readonly OptionsMenuSystem _optionsMenu;
    private readonly Action _resetSimulationPoses;

    private int? _rngSeedOverride;

    public string CurrentLevelPath { get; private set; } = LevelCatalog.DefaultLevelPath;
    public bool HighscoreIntermissionStarted { get; set; }
    public int CurrentRngSeed { get; private set; }

    public PlayLevelSession(
        MapData mapData,
        PlayerSystem playerSystem,
        DoorSystem doorSystem,
        EnemySystem enemySystem,
        PickupSystem pickupSystem,
        PlacedObjectSystem placedObjectSystem,
        ScoreSystem scoreSystem,
        ExitSystem exitSystem,
        SecretSystem secretSystem,
        RenderSystem renderSystem,
        SoundPropagationSystem soundPropagationSystem,
        HighscoreIntermission highscoreIntermission,
        HighscoreClient highscoreClient,
        EffectSystem effectSystem,
        RecordingSystem recordingSystem,
        FixedSimulationClock simulationClock,
        TickDiagnostics tickDiagnostics,
        InputSystem inputSystem,
        ControlsIntroSystem controlsIntro,
        OptionsMenuSystem optionsMenu,
        Action resetSimulationPoses)
    {
        _mapData = mapData;
        _playerSystem = playerSystem;
        _doorSystem = doorSystem;
        _enemySystem = enemySystem;
        _pickupSystem = pickupSystem;
        _placedObjectSystem = placedObjectSystem;
        _scoreSystem = scoreSystem;
        _exitSystem = exitSystem;
        _secretSystem = secretSystem;
        _renderSystem = renderSystem;
        _soundPropagationSystem = soundPropagationSystem;
        _highscoreIntermission = highscoreIntermission;
        _highscoreClient = highscoreClient;
        _effectSystem = effectSystem;
        _recordingSystem = recordingSystem;
        _simulationClock = simulationClock;
        _tickDiagnostics = tickDiagnostics;
        _inputSystem = inputSystem;
        _controlsIntro = controlsIntro;
        _optionsMenu = optionsMenu;
        _resetSimulationPoses = resetSimulationPoses;
    }

    public void SetRngSeedOverride(int? seed) => _rngSeedOverride = seed;

    public void OnEnter()
    {
        RebuildFeatureSystems();

        if (OperatingSystem.IsBrowser())
        {
            _highscoreClient.PrefetchLeaderboardAccess(CurrentLevelPath);
            _inputSystem.EnableMouse();
        }
        else if (!_controlsIntro.IsVisible)
            _inputSystem.DisableMouse();
        else
            _inputSystem.EnableMouse();

        _recordingSystem.ResetInputLatches();
        TryStartAutoRecording();
    }

    public ConsoleCommandResult LoadLevel(string pathOrName)
    {
        if (!LevelCatalog.TryResolve(pathOrName, out var resolvedPath, out var error))
            return ConsoleCommandResult.Fail(error);

        try
        {
            LevelSerializer.LoadFromJson(_mapData, Res.Path(resolvedPath));
            CurrentLevelPath = resolvedPath;
            ResetLevelState();
            return ConsoleCommandResult.Ok($"Loaded '{resolvedPath}'.");
        }
        catch (Exception ex)
        {
            return ConsoleCommandResult.Fail($"load: {ex.Message}");
        }
    }

    public ConsoleCommandResult RestartCurrentLevel()
    {
        try
        {
            LevelSerializer.LoadFromJson(_mapData, Res.Path(CurrentLevelPath));
            ResetLevelState();
            return ConsoleCommandResult.Ok($"Restarted from '{CurrentLevelPath}'.");
        }
        catch (Exception ex)
        {
            return ConsoleCommandResult.Fail($"restart: {ex.Message}");
        }
    }

    public ConsoleCommandResult ListPickupsForConsole()
    {
        var placements = _mapData.Pickups;
        if (placements.Count == 0)
            return ConsoleCommandResult.Ok($"No pickups in '{CurrentLevelPath}'.");

        var activeByTile = new HashSet<(int X, int Y)>(
            _pickupSystem.ActivePickups.Select(p => (p.TileX, p.TileY)));

        var rows = new List<string>(placements.Count);
        for (int i = 0; i < placements.Count; i++)
        {
            var placement = placements[i];
            int amount = PickupDefaults.GetAmount(placement.Type, placement.Amount);
            string amountText = placement.Amount == 0
                ? $"amount={amount} (default)"
                : $"amount={amount}";

            var world = LevelData.GetTileAnchorWorld(placement.TileX, placement.TileY, 1.5f);
            string active = activeByTile.Contains((placement.TileX, placement.TileY)) ? "yes" : "no";

            rows.Add(
                $"#{i} {placement.Type} tile=({placement.TileX},{placement.TileY}) {amountText} " +
                $"world=({world.X:F1},{world.Y:F1},{world.Z:F1}) active={active}");
        }

        int activeCount = _pickupSystem.ActivePickups.Count;
        string summary = placements.Count == 1
            ? $"1 pickup in '{CurrentLevelPath}' ({activeCount} active):"
            : $"{placements.Count} pickups in '{CurrentLevelPath}' ({activeCount} active):";

        return ConsoleCommandResult.Ok(summary, rows);
    }

    public void ResetLevelState()
    {
        // A level reset invalidates recording/replay tick indexing (sim clock restarts
        // at tick 0). The recording system's own restarts happen before it flags
        // recording/replay active, so this only fires for external resets.
        _recordingSystem.OnLevelStateReset();

        CurrentRngSeed = _rngSeedOverride ?? Random.Shared.Next();
        _enemySystem.SetRandomSeed(CurrentRngSeed);

        _playerSystem.ResetForLevelLoad(_mapData);
        RebuildFeatureSystems();
        _soundPropagationSystem.ClearPendingEvents();
        _highscoreIntermission.ResetForLevel();
        HighscoreIntermissionStarted = false;
        _effectSystem.Clear();
        _simulationClock.Reset();
        _tickDiagnostics.Reset();
        _resetSimulationPoses();

        if (OperatingSystem.IsBrowser())
            _highscoreClient.PrefetchLeaderboardAccess(CurrentLevelPath);

        TryStartAutoRecording();
    }

    private void RebuildFeatureSystems()
    {
        _doorSystem.Rebuild(_mapData.Doors, _mapData.Width);
        _enemySystem.Rebuild(_mapData.Enemies, _mapData);
        _pickupSystem.Rebuild(_mapData.Pickups, _mapData);
        _placedObjectSystem.Rebuild(_mapData);
        _scoreSystem.ResetForLevel(_mapData);
        _exitSystem.Rebuild(_mapData);
        _secretSystem.Rebuild(_mapData);
        _renderSystem.RebuildMeshes();
    }

    private void TryStartAutoRecording()
    {
        if (_recordingSystem.ShouldAutoRecordOnLevelReset)
            _recordingSystem.StartAutoRecording(_optionsMenu.Settings.MouseSensitivity);
    }
}
