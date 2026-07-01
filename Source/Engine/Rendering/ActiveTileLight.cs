using System.Numerics;

namespace Game.Engine.Rendering;

/// <summary>Placed light uploaded to the lighting shader for the current frame.</summary>
public readonly record struct ActiveTileLight(Vector3 Position, int RoomA, int RoomB = -1);
