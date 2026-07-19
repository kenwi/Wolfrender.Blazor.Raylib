using System.Numerics;

namespace Game.Engine.Rendering;

/// <summary>Shader/uniform snapshot for lighting diagnostics (consumed by DebugConsole).</summary>
public readonly record struct LightingDebugSnapshot(
    bool LightingShaderValid,
    int LightingShaderProgramId,
    bool SpriteLitShaderValid,
    int SpriteLitShaderProgramId,
    int ActiveUploadedLightCount,
    Vector3 PlayerPosition,
    float MaxLightDistance,
    float MinBrightness,
    float TileLightRadius,
    int TileLightCountLoc,
    int OcclusionMapLoc,
    int RoomMapLoc,
    int MapSizeLoc,
    int TileSizeLoc,
    int ApplySurfaceLightingLoc,
    int MeshRoomIdLoc,
    int[] TileLightLocs,
    int[] TileLightRoomLocs,
    Vector3[] ActiveLights,
    Vector2[] ActiveLightRooms,
    int OcclusionTextureId,
    int RoomTextureId);
