using System.Numerics;
using Game.Engine.Input;

namespace Game.Features.Recording;

public readonly record struct InputPollResult(InputState InputState, Vector2 MouseDelta);
