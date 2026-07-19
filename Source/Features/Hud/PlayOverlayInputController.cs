using Game.DebugConsole;
using Game.Features.Highscores;
using Game.Features.LevelProgress;
using Game.Features.Options;
using Game.Features.Players;
using Game.Features.Recording;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Hud;

/// <summary>
/// Play-mode overlay mouse policy and modal input stack (controls intro, options, console, highscores).
/// Returns whether the frame was fully handled so World can skip gameplay ticks.
/// </summary>
public sealed class PlayOverlayInputController
{
    private readonly ControlsIntroSystem _controlsIntro;
    private readonly ConsoleOverlay _consoleOverlay;
    private readonly OptionsMenuSystem _optionsMenu;
    private readonly HighscoreBoardOverlay _highscoreBoard;
    private readonly HighscoreIntermission _highscoreIntermission;
    private readonly RecordingSystem _recordingSystem;
    private readonly Player _player;
    private readonly ExitSystem _exitSystem;
    private readonly InputSystem _inputSystem;
    private readonly PlayOptionsFacade _playOptions;
    private readonly RuntimeConsoleService _runtimeConsole;
    private readonly Func<string, ConsoleCommandResult> _executeConsoleLine;
    private readonly Func<ConsoleCommandResult> _restartCurrentLevel;

    public PlayOverlayInputController(
        ControlsIntroSystem controlsIntro,
        ConsoleOverlay consoleOverlay,
        OptionsMenuSystem optionsMenu,
        HighscoreBoardOverlay highscoreBoard,
        HighscoreIntermission highscoreIntermission,
        RecordingSystem recordingSystem,
        Player player,
        ExitSystem exitSystem,
        InputSystem inputSystem,
        PlayOptionsFacade playOptions,
        RuntimeConsoleService runtimeConsole,
        Func<string, ConsoleCommandResult> executeConsoleLine,
        Func<ConsoleCommandResult> restartCurrentLevel)
    {
        _controlsIntro = controlsIntro;
        _consoleOverlay = consoleOverlay;
        _optionsMenu = optionsMenu;
        _highscoreBoard = highscoreBoard;
        _highscoreIntermission = highscoreIntermission;
        _recordingSystem = recordingSystem;
        _player = player;
        _exitSystem = exitSystem;
        _inputSystem = inputSystem;
        _playOptions = playOptions;
        _runtimeConsole = runtimeConsole;
        _executeConsoleLine = executeConsoleLine;
        _restartCurrentLevel = restartCurrentLevel;
    }

    public void SyncMouseCapture()
    {
        if (ShouldFreeMouseForOverlays())
        {
            if (!_inputSystem.IsMouseFree)
                _inputSystem.EnableMouse();
            return;
        }

        if (ShouldCaptureGameplayMouse() && _inputSystem.IsMouseFree)
            _inputSystem.RestoreGameplayMouse();
    }

    public void PollBrowserPointerLockEvents()
    {
        if (!OperatingSystem.IsBrowser())
            return;

        switch (BrowserPointerLockBridge.PollPointerLockEvent())
        {
            case "acquired":
                _inputSystem.OnBrowserPointerLockAcquired();
                break;
            case "failed":
                _inputSystem.OnBrowserPointerLockFailed();
                break;
            case "lost":
                HandleBrowserPointerLockLost(IsKeyDown(KeyboardKey.Escape));
                break;
        }
    }

    public void ArmBrowserMovementCapture()
    {
        bool waitingForGameplayStart = _controlsIntro.IsBlockingIntro && _inputSystem.IsMouseFree;
        bool armed = _inputSystem.IsMouseFree
            && (ShouldCaptureGameplayMouse() || waitingForGameplayStart);
        BrowserPointerLockBridge.MovementCaptureArmed = armed;
        BrowserPointerLockBridge.SetMovementCaptureArmed?.Invoke(armed);
    }

    public bool ShouldDeferGameplayMouseCapture() =>
        ShouldFreeMouseForOverlays()
        || _recordingSystem.IsReplaying
        || !_player.IsAlive
        || _exitSystem.IsLevelComplete;

    /// <summary>
    /// Handles overlay/modal input for this frame.
    /// Returns true when gameplay simulation should be skipped.
    /// </summary>
    public bool TryHandleOverlays(float deltaTime)
    {
        SyncMouseCapture();

        if (_controlsIntro.IsBlockingIntro)
        {
            PollBrowserPointerLockEvents();
            ArmBrowserMovementCapture();
            _inputSystem.Update(suppressClickToCapture: true);
            if (_controlsIntro.UpdateBlockingIntro(_inputSystem))
                return true;
        }

        if (_controlsIntro.IsManualOpen)
        {
            PollBrowserPointerLockEvents();
            _inputSystem.Update(suppressClickToCapture: true);
            if (IsKeyPressed(KeyboardKey.C) || IsKeyPressed(KeyboardKey.Escape))
                _controlsIntro.CloseManual(_inputSystem);
            return true;
        }

        bool toggledConsoleThisFrame = false;

        if (_consoleOverlay.IsOpen && IsKeyPressed(KeyboardKey.Escape))
        {
            _consoleOverlay.Close();
            if (!_optionsMenu.IsOpen)
                _inputSystem.RestoreGameplayMouse();
            return true;
        }

        if (_optionsMenu.IsOpen)
        {
            HandleOptionsMenuOpen();
            return true;
        }

        if (IsKeyPressed(KeyboardKey.Escape) && !_highscoreIntermission.CapturesEscapeKey)
        {
            if (_highscoreBoard.IsOpen)
            {
                _highscoreBoard.Close();
                SyncMouseCapture();
                return true;
            }

            if (_consoleOverlay.IsOpen)
                _consoleOverlay.Close();

            // Locked pointer: browser consumes ESC to exit pointer lock; menu opens via BrowserPointerLockBridge.
            if (OperatingSystem.IsBrowser() && !_inputSystem.IsMouseFree)
                return true;

            _optionsMenu.Open(_inputSystem);
            return true;
        }

        if (!_highscoreIntermission.BlocksConsoleToggle &&
            (IsKeyPressed(KeyboardKey.Grave) ||
            (OperatingSystem.IsBrowser() && IsKeyPressed(KeyboardKey.Period))))
        {
            _consoleOverlay.Toggle();
            toggledConsoleThisFrame = true;
            if (_consoleOverlay.IsOpen)
                _inputSystem.EnableMouse();
            else
                _inputSystem.RestoreGameplayMouse();
        }

        if (_consoleOverlay.IsOpen)
        {
            _consoleOverlay.UpdateInput(
                deltaTime,
                line => _executeConsoleLine(line),
                (line, cursor) => _runtimeConsole.GetCompletions(line, cursor),
                toggledConsoleThisFrame);
            return true;
        }

        if (_highscoreBoard.IsOpen)
        {
            _highscoreBoard.Update();
            SyncMouseCapture();
            return true;
        }

        if (CanToggleControlsLayout() && IsKeyPressed(KeyboardKey.C))
        {
            _controlsIntro.ToggleManual(_inputSystem);
            return true;
        }

        if (CanToggleHighscoreBoard() && IsKeyPressed(KeyboardKey.H))
        {
            _highscoreBoard.Toggle();
            SyncMouseCapture();
            return true;
        }

        if (CanInstantRestartLevel() && IsKeyPressed(KeyboardKey.R))
        {
            _ = _restartCurrentLevel();
            return true;
        }

        return false;
    }

    private void HandleOptionsMenuOpen()
    {
        var inputResult = _optionsMenu.HandleInput(GetScreenWidth(), GetScreenHeight());

        if (inputResult.WindowDisplayChanged)
        {
            _playOptions.ApplyWindowDisplay();
            _playOptions.ApplyGameResolution();
        }

        if (inputResult.GameResolutionChanged)
            _playOptions.ApplyGameResolution();
        if (inputResult.GraphicsChanged)
            _playOptions.ApplyGraphicsSettings();
        if (inputResult.ControlsChanged)
            _playOptions.ApplyControlSettings();
        if (inputResult.AudioChanged)
            _playOptions.ApplyAudioSettings();

        if (IsKeyPressed(KeyboardKey.Escape))
            _optionsMenu.Close(_inputSystem);
        else if (IsKeyPressed(KeyboardKey.Q))
            CloseWindow();
    }

    private void HandleBrowserPointerLockLost(bool escapeHeld)
    {
        _inputSystem.SyncPointerLockReleased();

        if (!escapeHeld
            || _consoleOverlay.IsOpen
            || _optionsMenu.IsOpen
            || _highscoreBoard.IsOpen
            || _highscoreIntermission.CapturesEscapeKey)
            return;

        _optionsMenu.Open(_inputSystem);
    }

    private bool CanToggleControlsLayout() =>
        !_controlsIntro.IsBlockingIntro
        && !_consoleOverlay.IsOpen
        && !_optionsMenu.IsOpen
        && !_highscoreBoard.IsOpen
        && !_highscoreIntermission.IsBlockingRestart
        && !_recordingSystem.IsReplaying
        && _player.IsAlive
        && !_exitSystem.IsLevelComplete;

    private bool CanToggleHighscoreBoard() =>
        !_controlsIntro.IsVisible
        && !_consoleOverlay.IsOpen
        && !_optionsMenu.IsOpen
        && !_highscoreIntermission.BlocksHighscoreBoardToggle;

    private bool CanInstantRestartLevel() =>
        !_controlsIntro.IsVisible
        && !_consoleOverlay.IsOpen
        && !_optionsMenu.IsOpen
        && !_highscoreBoard.IsOpen
        && !_highscoreIntermission.IsBlockingRestart
        && !_recordingSystem.IsReplaying
        && _player.IsAlive
        && !_exitSystem.IsLevelComplete;

    private bool ShouldFreeMouseForOverlays() =>
        _consoleOverlay.IsOpen
        || _optionsMenu.IsOpen
        || _highscoreBoard.IsOpen
        || _highscoreIntermission.IsLeaderboardInteractive
        || _controlsIntro.IsVisible;

    private bool ShouldCaptureGameplayMouse() =>
        !_recordingSystem.IsReplaying
        && _player.IsAlive
        && !_exitSystem.IsLevelComplete
        && !ShouldFreeMouseForOverlays();
}
