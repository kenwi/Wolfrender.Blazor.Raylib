using System.Numerics;
using Game.Engine.Simulation;
using Game.Features.Doors;
using Game.Features.Hud;
using Game.Features.Players;
using Game.Features.Recording;
using Game.Features.WorldObjects;

namespace Game.DebugConsole;

/// <summary>
/// Play-mode console command implementations (diagnostics, fly/fullbright, lighting dump, recording).
/// Level load/restart stay on World as lifecycle entry points.
/// </summary>
public sealed class PlayConsoleCommands
{
    private readonly TickDiagnostics _tickDiagnostics;
    private readonly RenderSystem _renderSystem;
    private readonly Player _player;
    private readonly MapData _mapData;
    private readonly DoorSystem _doorSystem;
    private readonly LightOcclusionMap _lightOcclusionMap;
    private readonly RecordingSystem _recordingSystem;
    private readonly ConsoleOverlay _consoleOverlay;
    private readonly InputSystem _inputSystem;
    private readonly ControlsIntroSystem _controlsIntro;
    private readonly Func<Vector3> _getRenderPosition;
    private readonly Func<string> _getCurrentLevelPath;

    public PlayConsoleCommands(
        TickDiagnostics tickDiagnostics,
        RenderSystem renderSystem,
        Player player,
        MapData mapData,
        DoorSystem doorSystem,
        LightOcclusionMap lightOcclusionMap,
        RecordingSystem recordingSystem,
        ConsoleOverlay consoleOverlay,
        InputSystem inputSystem,
        ControlsIntroSystem controlsIntro,
        Func<Vector3> getRenderPosition,
        Func<string> getCurrentLevelPath)
    {
        _tickDiagnostics = tickDiagnostics;
        _renderSystem = renderSystem;
        _player = player;
        _mapData = mapData;
        _doorSystem = doorSystem;
        _lightOcclusionMap = lightOcclusionMap;
        _recordingSystem = recordingSystem;
        _consoleOverlay = consoleOverlay;
        _inputSystem = inputSystem;
        _controlsIntro = controlsIntro;
        _getRenderPosition = getRenderPosition;
        _getCurrentLevelPath = getCurrentLevelPath;
    }

    public ConsoleCommandResult ToggleTickDiagnostics()
    {
        _tickDiagnostics.OverlayEnabled = !_tickDiagnostics.OverlayEnabled;
        return ConsoleCommandResult.Ok(
            _tickDiagnostics.OverlayEnabled
                ? "Tick diagnostics overlay enabled."
                : "Tick diagnostics overlay disabled.");
    }

    public ConsoleCommandResult SetTickDiagnostics(bool enabled)
    {
        _tickDiagnostics.OverlayEnabled = enabled;
        return ConsoleCommandResult.Ok(
            enabled
                ? "Tick diagnostics overlay enabled."
                : "Tick diagnostics overlay disabled.");
    }

    public ConsoleCommandResult GetTickDiagnosticsStatus() =>
        ConsoleCommandResult.Ok(_tickDiagnostics.BuildStatusLine());

    public ConsoleCommandResult ToggleStaticMeshes()
    {
        _renderSystem.UseStaticMeshes = !_renderSystem.UseStaticMeshes;
        return ConsoleCommandResult.Ok(BuildStaticMeshesStatusMessage());
    }

    public ConsoleCommandResult SetStaticMeshes(bool enabled)
    {
        _renderSystem.UseStaticMeshes = enabled;
        return ConsoleCommandResult.Ok(BuildStaticMeshesStatusMessage());
    }

    public ConsoleCommandResult GetStaticMeshesStatus() =>
        ConsoleCommandResult.Ok(BuildStaticMeshesStatusMessage());

    public ConsoleCommandResult ToggleFlying()
    {
        _player.IsFlying = !_player.IsFlying;
        if (_player.IsFlying)
            _player.Velocity = Vector3.Zero;
        return ConsoleCommandResult.Ok(BuildFlyingStatusMessage());
    }

    public ConsoleCommandResult SetFlying(bool enabled)
    {
        _player.IsFlying = enabled;
        if (!_player.IsFlying)
            _player.Velocity = Vector3.Zero;
        return ConsoleCommandResult.Ok(BuildFlyingStatusMessage());
    }

    public ConsoleCommandResult GetFlyingStatus() =>
        ConsoleCommandResult.Ok(BuildFlyingStatusMessage());

    public ConsoleCommandResult ToggleFullBright()
    {
        PrimitiveRenderer.SetFullBright(!PrimitiveRenderer.FullBright);
        return ConsoleCommandResult.Ok(BuildFullBrightStatusMessage());
    }

    public ConsoleCommandResult SetFullBright(bool enabled)
    {
        PrimitiveRenderer.SetFullBright(enabled);
        return ConsoleCommandResult.Ok(BuildFullBrightStatusMessage());
    }

    public ConsoleCommandResult GetFullBrightStatus() =>
        ConsoleCommandResult.Ok(BuildFullBrightStatusMessage());

    public ConsoleCommandResult DumpLightingCheck()
    {
        var renderPosition = _getRenderPosition();

        SceneLightingSetup.ApplyForView(
            _mapData,
            _lightOcclusionMap,
            _renderSystem.RoomMap,
            DoorTileEncoding.ForEngine,
            _doorSystem,
            renderPosition,
            _renderSystem.ComputeVisibleRooms);

        var shaderState = PrimitiveRenderer.GetLightingDebugSnapshot();
        var rows = LightingDiagnostics.BuildReport(
            _mapData,
            _renderSystem.RoomMap,
            _doorSystem,
            renderPosition,
            _lightOcclusionMap,
            shaderState);

        string levelPath = _getCurrentLevelPath();
        string summary = $"Lighting check for '{levelPath}':";
        string logPath = LightingReportWriter.Publish(summary, rows);

        var displayRows = new List<string>(rows.Count + 1)
        {
            $"Saved to: {logPath}"
        };
        displayRows.AddRange(rows);

        return ConsoleCommandResult.Ok($"{summary} (see terminal stderr or {logPath})", displayRows);
    }

    public ConsoleCommandResult StartRecording(string filename, float mouseSensitivity)
    {
        var result = _recordingSystem.StartRecording(filename, mouseSensitivity);
        if (result.Success)
        {
            _consoleOverlay.Close();
            _inputSystem.DisableMouse();
        }

        return result;
    }

    public ConsoleCommandResult StartReplay(string filename)
    {
        _controlsIntro.Dismiss();
        var result = _recordingSystem.StartReplay(filename);
        if (result.Success)
        {
            _consoleOverlay.Close();
            _inputSystem.DisableMouse();
        }

        return result;
    }

    public ConsoleCommandResult StartReplayRemote(int rank)
    {
        _controlsIntro.Dismiss();
        return _recordingSystem.ReplayRemote(rank);
    }

    public ConsoleCommandResult StartVerifyReplay(string filename)
    {
        _controlsIntro.Dismiss();
        var result = _recordingSystem.StartVerifyReplay(filename);
        if (result.Success)
        {
            _consoleOverlay.Close();
            _inputSystem.DisableMouse();
        }

        return result;
    }

    private string BuildStaticMeshesStatusMessage()
    {
        string mode = _renderSystem.UseStaticMeshes ? "on (room-scoped baked meshes)" : "off (legacy quads)";
        return $"Static meshes: {mode}. Baked wall quads: {_renderSystem.BakedQuadCount}.";
    }

    private static string BuildFullBrightStatusMessage() =>
        PrimitiveRenderer.FullBright
            ? "Fullbright: on (scene drawn at 100% brightness, torch and placed lights disabled)."
            : "Fullbright: off (normal distance and fixture lighting).";

    private string BuildFlyingStatusMessage()
    {
        if (!_player.IsFlying)
            return "Flying: off. Use Shift/Ctrl for vertical movement when enabled.";

        return $"Flying: on. Position Y={_player.Position.Y:F1}. Shift=up, Ctrl=down.";
    }
}
