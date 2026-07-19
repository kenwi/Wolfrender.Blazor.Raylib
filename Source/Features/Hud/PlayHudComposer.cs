using Game.DebugConsole;
using Game.Engine.Simulation;
using Game.Features.Animation;
using Game.Features.Combat;
using Game.Features.Doors;
using Game.Features.Highscores;
using Game.Features.LevelProgress;
using Game.Features.Options;
using Game.Features.Players;
using Game.Features.Recording;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Hud;

/// <summary>
/// Orders HUD texture passes and window-space overlays for play mode.
/// 3D scene rendering stays on World.
/// </summary>
public sealed class PlayHudComposer
{
    private readonly Player _player;
    private readonly ConsoleOverlay _consoleOverlay;
    private readonly OptionsMenuSystem _optionsMenu;
    private readonly ControlsIntroSystem _controlsIntro;
    private readonly ExitSystem _exitSystem;
    private readonly DoorSystem _doorSystem;
    private readonly WeaponSystem _weaponSystem;
    private readonly EffectSystem _effectSystem;
    private readonly ScoreSystem _scoreSystem;
    private readonly AnimationSystem _animationSystem;
    private readonly HighscoreBoardOverlay _highscoreBoard;
    private readonly HighscoreIntermission _highscoreIntermission;
    private readonly RecordingSystem _recordingSystem;
    private readonly TickDiagnostics _tickDiagnostics;
    private readonly MinimapSystem _minimapSystem;
    private readonly Func<InputState> _getInputState;
    private readonly Func<Camera3D> _getPlayerCamera;

    public PlayHudComposer(
        Player player,
        ConsoleOverlay consoleOverlay,
        OptionsMenuSystem optionsMenu,
        ControlsIntroSystem controlsIntro,
        ExitSystem exitSystem,
        DoorSystem doorSystem,
        WeaponSystem weaponSystem,
        EffectSystem effectSystem,
        ScoreSystem scoreSystem,
        AnimationSystem animationSystem,
        HighscoreBoardOverlay highscoreBoard,
        HighscoreIntermission highscoreIntermission,
        RecordingSystem recordingSystem,
        TickDiagnostics tickDiagnostics,
        MinimapSystem minimapSystem,
        Func<InputState> getInputState,
        Func<Camera3D> getPlayerCamera)
    {
        _player = player;
        _consoleOverlay = consoleOverlay;
        _optionsMenu = optionsMenu;
        _controlsIntro = controlsIntro;
        _exitSystem = exitSystem;
        _doorSystem = doorSystem;
        _weaponSystem = weaponSystem;
        _effectSystem = effectSystem;
        _scoreSystem = scoreSystem;
        _animationSystem = animationSystem;
        _highscoreBoard = highscoreBoard;
        _highscoreIntermission = highscoreIntermission;
        _recordingSystem = recordingSystem;
        _tickDiagnostics = tickDiagnostics;
        _minimapSystem = minimapSystem;
        _getInputState = getInputState;
        _getPlayerCamera = getPlayerCamera;
    }

    public void RenderHudToTexture(RenderTexture2D hudRenderTexture)
    {
        var inputState = _getInputState();
        int renderWidth = GameRenderSpace.HudTextureWidth;
        int renderHeight = GameRenderSpace.HudTextureHeight;
        bool consoleOpen = _consoleOverlay.IsOpen;
        bool optionsOpen = _optionsMenu.IsOpen;
        bool controlsIntroVisible = _controlsIntro.IsVisible;
        bool showWeaponView = _player.IsAlive && !consoleOpen && !optionsOpen && !controlsIntroVisible && !_exitSystem.IsBlockingGameplay;

        BeginTextureMode(hudRenderTexture);
        ClearBackground(new Color(0, 0, 0, 0));

        if (controlsIntroVisible)
        {
            ControlsIntroHud.Draw(renderWidth, renderHeight, _controlsIntro.IsBlockingIntro);
            EndTextureMode();
            return;
        }

        RenderNotificationOverlays(renderWidth, renderHeight, consoleOpen);

        if (!consoleOpen && !inputState.IsMouseFree && showWeaponView)
            PlaySessionOverlayHud.DrawReticle(_effectSystem, renderWidth, renderHeight);

        if (optionsOpen)
            OptionsMenuHud.Draw(_optionsMenu.Settings, renderWidth, renderHeight);

        if (!_player.IsAlive && !consoleOpen)
            PlaySessionOverlayHud.DrawGameOver(renderWidth, renderHeight);

        if (_highscoreBoard.IsOpen)
            _highscoreBoard.Draw(renderWidth, renderHeight);

        if (_highscoreIntermission.IsLeaderboardInteractive)
            _highscoreIntermission.DrawLeaderboard(renderWidth, renderHeight);

        EndTextureMode();
    }

    public void ComposeToScreen(Texture2D sceneTexture, Texture2D hudTexture)
    {
        int screenWidth = GetScreenWidth();
        int screenHeight = GetScreenHeight();

        BeginDrawing();
        ClearBackground(Color.Black);

        GameRenderSpace.DrawTextureToWindow(sceneTexture, screenWidth, screenHeight);
        GameRenderSpace.DrawTextureToWindow(hudTexture, screenWidth, screenHeight);

        RenderDebugLabels(screenWidth, screenHeight);
        RenderPlayHud(screenWidth, screenHeight);
        RenderMinimapAndDebugOverlays();

        _consoleOverlay.Render();

        EndDrawing();
    }

    private void RenderDebugLabels(int screenWidth, int screenHeight)
    {
        var inputState = _getInputState();
        DrawFPS(10, screenHeight - 120);

        var mouseLabel = _optionsMenu.IsOpen || inputState.IsMouseFree ? "MOUSE: FREE" : "MOUSE: LOCKED";
        var mouseColor = _optionsMenu.IsOpen || inputState.IsMouseFree ? Color.Green : Color.Red;
        int mouseLabelWidth = MeasureText(mouseLabel, 20);
        DrawText(mouseLabel, screenWidth - mouseLabelWidth - 10, 10, 20, mouseColor);

        var healthLabel = $"HEALTH: {(int)_player.Health} / {(int)_player.MaxHealth}";
        DrawText(healthLabel, 10, 40, 20, _player.IsAlive ? Color.RayWhite : Color.Red);

        if (_tickDiagnostics.OverlayEnabled)
            TickDiagnosticsHud.Draw(_tickDiagnostics);

        RecordingStatusHud.Draw(
            _recordingSystem.IsRecording,
            _recordingSystem.IsReplaying,
            screenWidth);
    }

    private void RenderPlayHud(int screenWidth, int screenHeight)
    {
        bool consoleOpen = _consoleOverlay.IsOpen;
        bool optionsOpen = _optionsMenu.IsOpen;
        bool inActiveLevel = _player.IsAlive && !_exitSystem.IsLevelComplete;
        bool showWeaponView = _player.IsAlive && !consoleOpen && !optionsOpen && !_exitSystem.IsBlockingGameplay;

        if (inActiveLevel)
        {
            LevelProgressOverlayHud.DrawScore(_scoreSystem, screenWidth);
            CombatOverlayHud.DrawInventory(_player);
        }

        if (showWeaponView)
            _animationSystem.RenderWeaponOverlay(screenWidth, screenHeight);

        _effectSystem.RenderScreenOverlay(screenWidth, screenHeight);
        RenderIntermissionOverlay(screenWidth, screenHeight, consoleOpen);
    }

    private void RenderNotificationOverlays(int renderWidth, int renderHeight, bool consoleOpen)
    {
        if (consoleOpen || _exitSystem.IsLevelComplete || !_player.IsAlive)
            return;

        if (_exitSystem.IsExitPending)
        {
            LevelProgressOverlayHud.DrawExitCountdown(_exitSystem.ExitCountdownRemaining, renderWidth, renderHeight);
            return;
        }

        if (_doorSystem.HasLockedHint)
            DoorOverlayHud.DrawLockedHint(_doorSystem, renderWidth, renderHeight);
        else if (_weaponSystem.HasNoAmmoHint)
            CombatOverlayHud.DrawNoAmmoHint(_weaponSystem, renderWidth, renderHeight);
    }

    private void RenderIntermissionOverlay(int screenWidth, int screenHeight, bool consoleOpen)
    {
        if (consoleOpen || !_exitSystem.IsLevelComplete)
            return;

        if (_highscoreIntermission.IsActive)
            _highscoreIntermission.Draw(_scoreSystem, screenWidth, screenHeight);
        else
            LevelProgressOverlayHud.DrawLevelComplete(
                _scoreSystem,
                screenWidth,
                screenHeight,
                showRestartHint: !_recordingSystem.IsReplaying);
    }

    private void RenderMinimapAndDebugOverlays()
    {
        var inputState = _getInputState();
        if (inputState.IsMinimapEnabled)
            _minimapSystem.Render(_player);

        int renderW = RenderData.InternalWidth;
        int renderH = RenderData.InternalHeight;
        Debug.DrawWorldOverlays(inputState.IsDebugEnabled, _getPlayerCamera(), renderW, renderH);
        Debug.Draw(inputState.IsDebugEnabled);
    }
}
