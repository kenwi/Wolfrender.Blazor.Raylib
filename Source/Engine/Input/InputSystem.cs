using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Engine.Input;

public class InputState
{
    public bool MoveForward { get; init; }
    public bool MoveBackward { get; init; }
    public bool MoveLeft { get; init; }
    public bool MoveRight { get; init; }
    public Vector2 MouseDelta { get; init; }
    public bool IsMouseFree { get; init; }
    public bool IsDebugEnabled { get; init; }
    public bool IsPaused { get; init; }
    public bool IsInteractPressed { get; init; }
    public bool IsChangeStatePressed { get; init; }
    public bool IsChangeAnimationPressed { get; set; }
    public bool IsMinimapEnabled { get; set; }
    public bool IsPrimaryFire { get; init; }

    /// <summary>Primary fire button held (not only pressed this frame).</summary>
    public bool IsPrimaryFireHeld { get; init; }

    /// <summary>1–4 when a weapon slot key was pressed this frame; 0 otherwise.</summary>
    public int WeaponSlotPressed { get; init; }

    public InputState WithoutInteract() => new()
    {
        MoveForward = MoveForward,
        MoveBackward = MoveBackward,
        MoveLeft = MoveLeft,
        MoveRight = MoveRight,
        MouseDelta = MouseDelta,
        IsMouseFree = IsMouseFree,
        IsDebugEnabled = IsDebugEnabled,
        IsPaused = IsPaused,
        IsInteractPressed = false,
        IsChangeStatePressed = IsChangeStatePressed,
        IsChangeAnimationPressed = IsChangeAnimationPressed,
        IsMinimapEnabled = IsMinimapEnabled,
        IsPrimaryFire = IsPrimaryFire,
        IsPrimaryFireHeld = IsPrimaryFireHeld,
        WeaponSlotPressed = WeaponSlotPressed
    };
}

public class InputSystem
{
    private bool _isMouseFree = true;
    private bool _isDebugEnabled = false;
    private bool _isMinimapEnabled = false;

    /// <summary>
    /// After warping the cursor to center, Raylib can report a large delta on the
    /// next poll(s). Skip rotation for those polls so capture starts from zero.
    /// </summary>
    private int _suppressMouseDeltaPolls;

    /// <summary>
    /// Browser pointer lock engages asynchronously after click; the warp delta often
    /// arrives on the first real movement instead of the capture frame.
    /// </summary>
    private bool _skipNextNonZeroMouseDelta;

    public bool IsMouseFree => _isMouseFree;

    /// <summary>Sync state when the browser releases pointer lock (e.g. ESC) without going through Raylib.</summary>
    public void SyncPointerLockReleased()
    {
        _isMouseFree = true;
        _skipNextNonZeroMouseDelta = false;
    }

    /// <summary>Browser pointer lock just engaged; flush and ignore the first movement delta.</summary>
    public void OnBrowserPointerLockAcquired()
    {
        GetMouseDelta();
        _skipNextNonZeroMouseDelta = true;
    }

    /// <summary>After closing an overlay: desktop re-captures; browser waits for click-to-capture.</summary>
    public void RestoreGameplayMouse()
    {
        if (OperatingSystem.IsBrowser())
            EnableMouse();
        else
            DisableMouse();
    }

    public void Update(bool suppressClickToCapture = false)
    {
        // Click-to-capture: locks the cursor on first click so the browser's
        // pointer lock API receives the required user gesture. Also gives desktop
        // users a natural "click to play" entry point after toggling mouse free.
        if (!suppressClickToCapture && _isMouseFree && IsMouseButtonPressed(MouseButton.Left))
        {
            DisableMouse();
        }

        if (IsKeyPressed(KeyboardKey.L))
        {
            _isMouseFree = !_isMouseFree;
            if (_isMouseFree)
            {
                ShowCursor();
            }
            else
            {
                HideCursor();
            }
        }

        if (IsKeyPressed(KeyboardKey.I))
        {
            _isDebugEnabled = !_isDebugEnabled;
        }

        if (IsKeyPressed(KeyboardKey.M))
        {
            ToggleMouse();
        }
    }

    public void ToggleMouse()
    {
        _isMouseFree = !_isMouseFree;
        if (_isMouseFree)
        {
            EnableCursor();
        }
        else
        {
            DisableCursor();
            RecenterCapturedMouse();
        }
    }

    public void DisableMouse()
    {
        _isMouseFree = false;
        DisableCursor();
        RecenterCapturedMouse();
    }

    public void EnableMouse()
    {
        _isMouseFree = true;
        _skipNextNonZeroMouseDelta = false;
        EnableCursor();
    }

    public void CenterMouse()
    {
        if (!_isMouseFree)
            RecenterCapturedMouse();
    }

    private void RecenterCapturedMouse()
    {
        SetMousePosition(GetScreenWidth() / 2, GetScreenHeight() / 2);
        GetMouseDelta();
        if (OperatingSystem.IsBrowser())
            _skipNextNonZeroMouseDelta = true;
        else
            _suppressMouseDeltaPolls = 2;
    }

    public InputState GetInputState()
    {
        Vector2 mouseDelta = GetMouseDelta();
        if (_suppressMouseDeltaPolls > 0)
        {
            _suppressMouseDeltaPolls--;
            mouseDelta = Vector2.Zero;
        }
        else if (_skipNextNonZeroMouseDelta &&
                 (Math.Abs(mouseDelta.X) > 0.001f || Math.Abs(mouseDelta.Y) > 0.001f))
        {
            _skipNextNonZeroMouseDelta = false;
            mouseDelta = Vector2.Zero;
        }

        return new InputState
        {
            MoveForward = IsKeyDown(KeyboardKey.W),
            MoveBackward = IsKeyDown(KeyboardKey.S),
            MoveLeft = IsKeyDown(KeyboardKey.A),
            MoveRight = IsKeyDown(KeyboardKey.D),
            MouseDelta = mouseDelta,
            IsMouseFree = _isMouseFree,
            IsPaused = false,
            IsDebugEnabled = _isDebugEnabled,
            IsMinimapEnabled = _isMinimapEnabled,
            IsInteractPressed =  IsKeyPressed(KeyboardKey.E),
            IsChangeStatePressed = IsKeyPressed(KeyboardKey.C),
            IsChangeAnimationPressed = IsKeyPressed(KeyboardKey.V),
            IsPrimaryFire = !_isMouseFree && IsMouseButtonPressed(MouseButton.Left),
            IsPrimaryFireHeld = !_isMouseFree && IsMouseButtonDown(MouseButton.Left),
            WeaponSlotPressed = ReadWeaponSlotPressed(),
        };
    }

    private static int ReadWeaponSlotPressed()
    {
        if (IsKeyPressed(KeyboardKey.One))
            return 1;
        if (IsKeyPressed(KeyboardKey.Two))
            return 2;
        if (IsKeyPressed(KeyboardKey.Three))
            return 3;
        if (IsKeyPressed(KeyboardKey.Four))
            return 4;
        return 0;
    }

    public Vector3 GetMoveDirection(Camera3D camera) =>
        GetMoveDirection(camera, GetInputState());

    public Vector3 GetMoveDirection(Camera3D camera, InputState input)
    {
        Vector3 direction = Vector3.Zero;

        Vector3 targetToPosition = camera.Target - camera.Position;
        float targetDistance = targetToPosition.Length();

        if (targetDistance < 0.001f)
            return Vector3.Zero;

        Vector3 forward = targetToPosition / targetDistance;

        Vector3 forwardHorizontal = new Vector3(forward.X, 0, forward.Z);
        float forwardLength = forwardHorizontal.Length();

        if (forwardLength > 0.001f)
        {
            forwardHorizontal = forwardHorizontal / forwardLength;
        }
        else
        {
            forwardHorizontal = Vector3.UnitZ;
        }

        Vector3 rightVec = Vector3.Cross(forwardHorizontal, -Vector3.UnitY);
        float rightLength = rightVec.Length();
        Vector3 right = rightLength > 0.001f ? rightVec / rightLength : Vector3.UnitX;

        if (input.MoveForward)
            direction += forwardHorizontal;
        if (input.MoveBackward)
            direction -= forwardHorizontal;
        if (input.MoveRight)
            direction -= right;
        if (input.MoveLeft)
            direction += right;

        float directionLength = direction.Length();
        if (directionLength > 0.001f)
            return direction / directionLength;

        return Vector3.Zero;
    }
}

