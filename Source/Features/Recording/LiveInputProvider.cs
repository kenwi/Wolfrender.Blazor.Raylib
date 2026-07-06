using System.Numerics;
using Game.Engine.Input;

namespace Game.Features.Recording;

/// <summary>
/// Bridges Raylib's per-render-frame input to the fixed simulation tick.
///
/// Raylib "pressed" edges and the mouse delta are valid for one render frame,
/// but a frame can run 0..N simulation ticks. Sampling once per frame and
/// latching until the next poll guarantees:
/// - a press during a 0-tick frame is consumed by the next tick (never dropped)
/// - one press triggers exactly one tick (never doubled on multi-tick frames)
/// - the frame's mouse delta is applied by exactly one tick (never re-applied)
/// This makes live input semantics identical to replayed input semantics.
/// </summary>
public sealed class LiveInputProvider : IInputProvider
{
    private readonly InputSystem _inputSystem;

    private InputState _frameState = new();
    private bool _hasFrameSample;

    private Vector2 _pendingMouseDelta;
    private bool _pendingInteract;
    private int _pendingWeaponSlot;
    private bool _pendingFirePress;
    private bool _fireHeldLastPoll;

    public LiveInputProvider(InputSystem inputSystem)
    {
        _inputSystem = inputSystem;
    }

    /// <summary>Sample Raylib once per render frame, before the simulation tick loop.</summary>
    public void BeginFrame()
    {
        var state = _inputSystem.GetInputState();
        _frameState = state;
        _hasFrameSample = true;

        if (!state.IsMouseFree)
            _pendingMouseDelta += state.MouseDelta;

        _pendingInteract |= state.IsInteractPressed;
        _pendingFirePress |= state.IsPrimaryFire;
        if (state.WeaponSlotPressed != 0)
            _pendingWeaponSlot = state.WeaponSlotPressed;
    }

    /// <summary>Clear latched input, e.g. when gameplay resumes after an overlay.</summary>
    public void ResetLatches()
    {
        _pendingMouseDelta = Vector2.Zero;
        _pendingInteract = false;
        _pendingWeaponSlot = 0;
        _pendingFirePress = false;
        _fireHeldLastPoll = false;
        _hasFrameSample = false;
    }

    public InputPollResult Poll(float deltaTime)
    {
        if (!_hasFrameSample)
            BeginFrame();

        var frame = _frameState;

        // Include the latched press so a click shorter than a frame still
        // registers as held for exactly one tick.
        bool fireHeld = frame.IsPrimaryFireHeld || _pendingFirePress;
        bool firePressedEdge = fireHeld && !_fireHeldLastPoll;
        _fireHeldLastPoll = fireHeld;

        var mouseDelta = frame.IsMouseFree ? Vector2.Zero : _pendingMouseDelta;

        var state = new InputState
        {
            MoveForward = frame.MoveForward,
            MoveBackward = frame.MoveBackward,
            MoveLeft = frame.MoveLeft,
            MoveRight = frame.MoveRight,
            MouseDelta = mouseDelta,
            IsMouseFree = frame.IsMouseFree,
            IsPaused = frame.IsPaused,
            IsDebugEnabled = frame.IsDebugEnabled,
            IsMinimapEnabled = frame.IsMinimapEnabled,
            IsInteractPressed = _pendingInteract,
            IsChangeStatePressed = frame.IsChangeStatePressed,
            IsChangeAnimationPressed = frame.IsChangeAnimationPressed,
            IsPrimaryFire = firePressedEdge,
            IsPrimaryFireHeld = fireHeld,
            WeaponSlotPressed = _pendingWeaponSlot
        };

        _pendingMouseDelta = Vector2.Zero;
        _pendingInteract = false;
        _pendingWeaponSlot = 0;
        _pendingFirePress = false;

        return new InputPollResult(state, mouseDelta);
    }
}
