using System.Numerics;
using Game.Entities;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Systems;

public class InputState
{
    public bool MoveForward { get; init; }
    public bool MoveBackward { get; init; }
    public bool MoveLeft { get; init; }
    public bool MoveRight { get; init; }
    public Vector2 MouseDelta { get; init; }
    public bool IsMouseFree { get; init; }
    public bool IsDebugEnabled { get; init; }
    public bool IsGamePaused { get; init; }
    public bool IsInteractPressed { get; init; }
    public bool IsChangeStatePressed { get; init; }
    public bool IsChangeAnimationPressed { get; set; }
    public bool IsMinimapEnabled { get; set; }
}

public class InputSystem
{
    private bool _isMouseFree = true;
    private bool _isGamePaused = true;
    private bool _isDebugEnabled = false;
    private bool _isMinimapEnabled = false;

    public void Update()
    {
        // Click-to-capture: locks the cursor on first click so the browser's
        // pointer lock API receives the required user gesture. Also gives desktop
        // users a natural "click to play" entry point after toggling mouse free.
        if (_isMouseFree && IsMouseButtonPressed(MouseButton.Left))
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

        if (IsKeyPressed(KeyboardKey.P))
        {
            _isGamePaused = !_isGamePaused;
            ToggleMouse();
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
        }
    }

    public void DisableMouse()
    {
        _isMouseFree = false;
        DisableCursor();
    }

    public void EnableMouse()
    {
        _isMouseFree = true;
        EnableCursor();
    }

    public void CenterMouse()
    {
        if (!_isMouseFree)
        {
            SetMousePosition(GetScreenWidth() / 2, GetScreenHeight() / 2);
        }
    }

    public InputState GetInputState()
    {
        return new InputState
        {
            MoveForward = IsKeyDown(KeyboardKey.W),
            MoveBackward = IsKeyDown(KeyboardKey.S),
            MoveLeft = IsKeyDown(KeyboardKey.A),
            MoveRight = IsKeyDown(KeyboardKey.D),
            MouseDelta = GetMouseDelta(),
            IsMouseFree = _isMouseFree,
            IsGamePaused = _isGamePaused,
            IsDebugEnabled = _isDebugEnabled,
            IsMinimapEnabled = _isMinimapEnabled,
            IsInteractPressed =  IsKeyPressed(KeyboardKey.E),
            IsChangeStatePressed = IsKeyPressed(KeyboardKey.C),
            IsChangeAnimationPressed = IsKeyPressed(KeyboardKey.V),
        };
    }

    public Vector3 GetMoveDirection(Player player)
    {
        Vector3 direction = Vector3.Zero;

        // Calculate forward direction (camera look direction)
        Vector3 targetToPosition = player.Camera.Target - player.Camera.Position;
        float targetDistance = targetToPosition.Length();

        if (targetDistance < 0.001f)
            return Vector3.Zero;

        Vector3 forward = targetToPosition / targetDistance;

        // Project forward onto horizontal plane
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

        // Calculate right direction
        Vector3 rightVec = Vector3.Cross(forwardHorizontal, -Vector3.UnitY);
        float rightLength = rightVec.Length();
        Vector3 right = rightLength > 0.001f ? rightVec / rightLength : Vector3.UnitX;

        if (IsKeyDown(KeyboardKey.W))
            direction += forwardHorizontal;
        if (IsKeyDown(KeyboardKey.S))
            direction -= forwardHorizontal;
        if (IsKeyDown(KeyboardKey.D))
            direction -= right;
        if (IsKeyDown(KeyboardKey.A))
            direction += right;

        float directionLength = direction.Length();
        if (directionLength > 0.001f)
        {
            return direction / directionLength;
        }

        return Vector3.Zero;
    }
}

